using System.IO.Pipes;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Infrastructure;

public sealed class NamedPipeClient : INamedPipeClient
{
    private readonly ClientId _clientId;

    private readonly Subject<ClientConnectionStateChange> _connectionChanged = new();
    private readonly Subject<Coordinate> _coordinates = new();
    private readonly Subject<ServerSendsConfigurationMessage> _configuration = new();
    private readonly CancellationTokenSource _cts = new();
    private StreamReader? _reader;
    private Task? _readLoop;
    private NamedPipeClientStream? _stream;
    private StreamWriter? _writer;

    public NamedPipeClient(ClientId clientId) => _clientId = clientId;

    /// <inheritdoc />
    public IObservable<ClientConnectionStateChange> ConnectionChanged => _connectionChanged.AsObservable();

    /// <inheritdoc />
    public IObservable<Coordinate> Coordinates => _coordinates.AsObservable();

    /// <inheritdoc />
    public IObservable<ServerSendsConfigurationMessage> ConfigurationReceived => _configuration.AsObservable();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _connectionChanged?.OnCompleted();
        _coordinates?.OnCompleted();
        _configuration?.OnCompleted();
        _connectionChanged.Dispose();
        _coordinates.Dispose();
        _configuration.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _connectionChanged?.OnCompleted();
        _coordinates?.OnCompleted();
        _configuration?.OnCompleted();
        _connectionChanged.Dispose();
        _coordinates.Dispose();
        _configuration.Dispose();
        GC.SuppressFinalize(this);
    }


    /// <inheritdoc />
    public async Task ConnectAsync(TimeSpan? retryDelay = null, CancellationToken ct = default)
    {
        retryDelay ??= TimeSpan.FromMilliseconds(500);
        while (!ct.IsCancellationRequested && !_cts.IsCancellationRequested)
        {
            try
            {
                var client = new NamedPipeClientStream(
                    ".",
                    PipeProtocol.DiscoveryPipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await client.ConnectAsync((int)TimeSpan.FromSeconds(2).TotalMilliseconds, ct).ConfigureAwait(false);

                _stream = client;
                _reader = new StreamReader(client, Encoding.UTF8, false, leaveOpen: true);
                _writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };

                // Send Hello with clientId
                var hello = new ClientHelloMessage { ClientId = _clientId.Id };
                await _writer.WriteLineAsync(PipeSerialization.SerializeFromClient(hello)).ConfigureAwait(false);

                _connectionChanged.OnNext(new ClientConnectionStateChange(ConnectionState.Connected));

                _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
                return;
            }
            catch (TimeoutException)
            {
                var x = 1;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // other errors, retry
            }

            await Task.Delay(retryDelay.Value, ct).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    public async Task SendByeAsync(CancellationToken ct = default)
    {
        if (_writer is { } w)
        {
            try
            {
                var bye = new ClientByeMessage { ClientId = _clientId.Id };
                await w.WriteLineAsync(PipeSerialization.SerializeFromClient(bye)).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        // TODO: this is fantastic :D
        // Let's keep it this way and let people marvel at it.
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // ignored
        }

        try
        {
            await SendByeAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            if (_readLoop != null)
            {
                await _readLoop.ConfigureAwait(false);
            }
        }
        catch
        {
        }

        try
        {
            _writer?.Dispose();
        }
        catch
        {
        }

        try
        {
            _reader?.Dispose();
        }
        catch
        {
        }

        try
        {
            _stream?.Dispose();
        }
        catch
        {
        }
    }

    /// <inheritdoc />
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            if (_reader is null || _stream is null)
            {
                return;
            }

            while (!ct.IsCancellationRequested && _stream.IsConnected)
            {
                var line = await _reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                var msg = PipeSerialization.DeserializeForClient(line);
                if (msg is ServerSendsCoordinateMessage coord && coord.ClientId == _clientId.Id)
                {
                    _coordinates.OnNext(new Coordinate(coord.X, coord.Y));
                }
                else if (msg is ServerSendsConfigurationMessage cfg && cfg.ClientId == _clientId.Id)
                {
                    _configuration.OnNext(cfg);
                }
                else if (msg is ServerRequestsClientCloseMessage close && close.ClientId == _clientId.Id)
                {
                    // Attempt graceful bye and then exit the process
                    _ = Task.Run(async () =>
                    {
                        // TODO: pass to client as event and use ApplicationLifetime
                        try
                        {
                            await SendByeAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                        }

                        try
                        {
                            await DisconnectAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                        }

                        try
                        {
                            Environment.Exit(0);
                        }
                        catch
                        {
                        }
                    });
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            _connectionChanged.OnNext(new ClientConnectionStateChange(ConnectionState.Disconnected));
        }
    }
}
