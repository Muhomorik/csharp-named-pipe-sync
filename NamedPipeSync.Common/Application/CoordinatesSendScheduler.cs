using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;
using NLog;

namespace NamedPipeSync.Common.Application;

/// <summary>
/// Periodically calculates and sends coordinates to connected clients.
/// - On each tick (Rx timer driven by provided <see cref="IScheduler"/>), it fetches a snapshot of all
///   <see cref="ClientWithRuntime"/> from <see cref="IClientWithRuntimeRepository"/>,
///   calculates the next position via <see cref="ICoordinatesCalculator"/>,
///   sends the coordinates via <see cref="INamedPipeServer"/>,
///   and persists the updated runtime entity back to the repository.
///
/// This component is part of the Application layer (orchestration only). It has no UI/process lifetime control.
/// </summary>
public sealed class CoordinatesSendScheduler : ICoordinatesSendScheduler, IDisposable, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly INamedPipeServer _server;
    private readonly IClientWithRuntimeRepository _repository;
    private readonly ICoordinatesCalculator _calculator;
    private readonly IScheduler _scheduler;

    private readonly int _stepPixels;

    private TimeSpan _interval;
    private IDisposable? _subscription;
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private int _started; // 0 = stopped, 1 = started
    private bool _disposed;

    /// <summary>
    /// Creates a new scheduler.
    /// </summary>
    /// <param name="logger">NLog logger resolved from DI. Must not be null.</param>
    /// <param name="interval">Initial send interval. Must be greater than TimeSpan.Zero.</param>
    /// <param name="scheduler">Rx scheduler used for the timer. For production, prefer TaskPoolScheduler or EventLoopScheduler.</param>
    /// <param name="server">Named pipe server used to send coordinates. Must not be null.</param>
    /// <param name="repository">Runtime repository providing clients. Must not be null.</param>
    /// <param name="calculator">Coordinate calculator used to advance client positions. Must not be null.</param>
    /// <param name="stepPixels">Movement step in pixels for each tick. Must be >= 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">If interval is not positive or stepPixels is negative.</exception>
    /// <exception cref="ArgumentNullException">If any required dependency is null.</exception>
    public CoordinatesSendScheduler(
        ILogger logger,
        TimeSpan interval,
        IScheduler scheduler,
        INamedPipeServer server,
        IClientWithRuntimeRepository repository,
        ICoordinatesCalculator calculator,
        int stepPixels)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        if (stepPixels < 0) throw new ArgumentOutOfRangeException(nameof(stepPixels));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _interval = interval;
        _stepPixels = stepPixels;
    }

    /// <summary>
    /// Starts periodic sending if not already started. Idempotent.
    /// </summary>
    public void StartSending()
    {
        ThrowIfDisposed();
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return; // already started
        }
        // Create subscription with current interval
        _subscription = Observable.Interval(_interval, _scheduler)
            .Subscribe(__ => { _ = ProcessTickSafeAsync(); });
    }

    /// <summary>
    /// Stops periodic sending. Safe to call multiple times.
    /// </summary>
    public void StopSending()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            // already stopped
        }
        _subscription?.Dispose();
        _subscription = null;
    }

    /// <summary>
    /// Resets each client's position to its last checkpoint and marks it as being on a checkpoint.
    /// Sends the reset coordinates to connected clients.
    /// </summary>
    public async Task ResetPositionAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        try
        {
            var clients = _repository.GetAll();
            var now = DateTimeOffset.UtcNow;
            foreach (var client in clients)
            {
                ct.ThrowIfCancellationRequested();
                // Reset runtime state to the last checkpoint location
                client.Coordinates = client.LastCheckpoint.Location;
                client.IsOnCheckpoint = true;
                // Inform client if connected
                if (_server.IsClientConnected(client.Id))
                {
                    await _server.SendCoordinateAsync(client.Id, client.Coordinates.X, client.Coordinates.Y, ct).ConfigureAwait(false);
                }
                _repository.Update(client);
            }
        }
        catch (OperationCanceledException)
        {
            // bubble cancellation
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
        }
    }

    /// <summary>
    /// Changes the timer interval. If currently running, restarts the timer with the new interval.
    /// </summary>
    public void ChangeTimer(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        ThrowIfDisposed();
        _interval = interval;
        var wasRunning = Volatile.Read(ref _started) == 1;
        if (wasRunning)
        {
            StopSending();
            StartSending();
        }
    }


    private async Task ProcessTickSafeAsync()
    {
        if (!_tickGate.Wait(0))
        {
            // Previous tick still running; skip to avoid overlap
            return;
        }
        try
        {
            await ProcessTickAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignored; no cancellation token here
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
        }
        finally
        {
            _tickGate.Release();
        }
    }

    private async Task ProcessTickAsync()
    {
        var clients = _repository.GetAll();
        if (clients.Count == 0) return;
        var now = DateTimeOffset.UtcNow;
        foreach (var client in clients)
        {
            // Advance position
            var next = _calculator.NextCoordinates(now, client, _stepPixels);

            // Send only if connected per contract
            if (_server.IsClientConnected(client.Id))
            {
                try
                {
                    await _server.SendCoordinateAsync(client.Id, next.X, next.Y).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex);
                }
            }

            // Persist updated runtime entity
            _repository.Update(client);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CoordinatesSendScheduler));
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopSending();
        _tickGate.Dispose();
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
