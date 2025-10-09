using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Domain.Events;
using NamedPipeSync.Common.Infrastructure.Protocol;
using NamedPipeSync.Server.Services;
using NLog;
using NamedPipeSync.Common.Application.Imaging;

namespace NamedPipeSync.Server.Models;

/// <summary>
///     SERVER. Default implementation of <see cref="IMainWindowServerModel"/>.
///     Contains non-UI orchestration used by the Server Main Window.
/// </summary>
public sealed class MainWindowServerModel : IMainWindowServerModel
{
    private string _lastScreenshotBase64 = string.Empty;
    private readonly ILogger _logger;
    private readonly INamedPipeServer _server;
    private readonly IClientWithRuntimeRepository _repository;
    private readonly IClientWithRuntimeEventDispatcher _events;
    private readonly IClientProcessLauncher _launcher;
    private readonly ICoordinatesSendScheduler _scheduler;
    private readonly IImageBase64Converter _imageBase64Converter;
    private readonly IWindowStateService _windowStateService;
    private readonly IScreenCaptureService _screenCaptureService;

    private ShowMode _currentShowMode = ShowMode.Debugging;

    public IObservable<ClientConnectionChange> ConnectionChanged => _server.ConnectionChanged;
    public IObservable<ClientWithRuntimeEvent> Events => _events.Events;

    public ShowMode CurrentShowMode
    {
        get => _currentShowMode;
        set => _currentShowMode = value;
    }

    /// <summary>
    ///     DI constructor. Keep non-UI and do not terminate process here.
    /// </summary>
    /// <param name="logger">NLog logger resolved from DI. Must not be null.</param>
    /// <param name="server">Named pipe server instance. Must not be null.</param>
    /// <param name="repository">Runtime repository for clients. Must not be null.</param>
    /// <param name="events">Domain event dispatcher. Must not be null.</param>
    /// <param name="launcher">Client process launcher. Must not be null.</param>
    [UsedImplicitly]
    public MainWindowServerModel(
        ILogger logger,
        INamedPipeServer server,
        IClientWithRuntimeRepository repository,
        IClientWithRuntimeEventDispatcher events,
        IClientProcessLauncher launcher,
        ICoordinatesSendScheduler scheduler,
        IImageBase64Converter imageBase64Converter,
        IWindowStateService windowStateService,
        IScreenCaptureService screenCaptureService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _imageBase64Converter = imageBase64Converter ?? throw new ArgumentNullException(nameof(imageBase64Converter));
        _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
        _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
    }

    public void StartServer() => _server.Start();

    public void SeedMissingClients()
    {
        // Ensure repository has been initialized / seeded; repository will
        // populate from checkpoints if its internal map is empty.
        _repository.GetAll();
    }

    public void EnsureClientEntryOnConnectionChange(ClientId clientId, ConnectionState state)
    {
        if (!_repository.TryGet(clientId, out var client))
        {
            // Seed using checkpoint with same id when available, otherwise use first checkpoint
            var cp = Checkpoints.Start.FirstOrDefault(c => c.Id == clientId.Id);
            if (cp.Id == 0 && Checkpoints.Start.Count > 0)
            {
                cp = Checkpoints.Start[0];
            }

            // Determine render checkpoint as the configured checkpoint at index + 2 (wrap-around).
            var cps = Checkpoints.Start;
            Checkpoint renderCp;
            if (cps.Count > 0)
            {
                var idx = Math.Max(0, cps.ToList().FindIndex(c => c.Id == cp.Id));
                renderCp = cps[(idx + 2) % cps.Count];
            }
            else
            {
                // No configured checkpoints; fall back to the selected cp.
                renderCp = cp;
            }

            client = new ClientWithRuntime(clientId, cp, renderCp);
        }

        client!.Connection = state;
        _repository.Update(client);
    }

    public IReadOnlyCollection<ClientWithRuntime> GetAllClients() => _repository.GetAll();
    public bool TryGet(ClientId clientId, out ClientWithRuntime? client) => _repository.TryGet(clientId, out client);

    public void StartClient(ClientId id) => _launcher.StartClient(id);

    public Task StartManyAsync(IEnumerable<ClientId> ids, CancellationToken ct = default) => _launcher.StartManyAsync(ids, ct);

    public async Task CloseAllClientsAsync(CancellationToken ct = default)
    {
        try
        {
            var ids = _server.ConnectedClientIds;
            var tasks = ids.Select(id => _server.SendCloseRequestAsync(id, ct));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
        }
    }

    public void StartSending()
    {
        _scheduler.StartSending();
    }

    public void StopSending()
    {
        _scheduler.StopSending();
    }

    public Task ResetPositionAsync(CancellationToken ct = default)
    {
        return _scheduler.ResetPositionAsync(ct);
    }

    public IReadOnlyCollection<ClientId> GetCurrentlyConnectedClientIds()
    {
        return _server.ConnectedClientIds;
    }

    public async Task CaptureScreenAndRestartClientsAsync(CancellationToken ct = default)
    {
        try
        {
            // Determine which clients were connected prior to capture via authoritative source (server).
            var connectedIds = _server.ConnectedClientIds.ToArray();

            // Request to close all clients before capturing.
            await CloseAllClientsAsync(ct).ConfigureAwait(false);

            // Minimize window to avoid capturing app UI, then capture the screen.
            await _windowStateService.MinimizeAsync().ConfigureAwait(false);
            var pngBytes = await _screenCaptureService.CaptureCurrentScreenPngAsync().ConfigureAwait(false);
            _logger.Trace("Screenshot captured, size={0} bytes", pngBytes?.Length ?? 0);

            // Restore window after capture.
            await _windowStateService.RestoreAsync().ConfigureAwait(false);

            // Delegate processing and restart.
            await ProcessScreenshotAndRestartAsync(pngBytes, connectedIds, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
        }
    }

    public async Task<string> ProcessScreenshotAndRestartAsync(byte[] pngBytes, IEnumerable<ClientId> reconnectIds, CancellationToken ct = default)
    {
        try
        {
            var base64 = (pngBytes is { Length: > 0 })
                ? _imageBase64Converter.PngBytesToBase64(pngBytes)
                : string.Empty;
            _lastScreenshotBase64 = base64;

            var ids = reconnectIds?.ToArray() ?? Array.Empty<ClientId>();
            if (ids.Length > 0)
            {
                await _launcher.StartManyAsync(ids, ct).ConfigureAwait(false);
            }

            return base64;
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            return string.Empty;
        }
    }
}
