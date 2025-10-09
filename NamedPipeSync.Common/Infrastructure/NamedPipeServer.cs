using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Channels;

using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Infrastructure;

/// <inheritdoc cref="INamedPipeServer" />
public sealed class NamedPipeServer : INamedPipeServer
{
    private readonly Lazy<IServerConfigurationProvider>? _configurationProvider;

    public NamedPipeServer()
    {
    }

    public NamedPipeServer(Lazy<IServerConfigurationProvider> configurationProvider)
    {
        _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
    }
    private readonly ConcurrentDictionary<ClientId, ClientConnection> _clients = new();

    private readonly Subject<ClientConnectionChange> _connectionChanged = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;

    public IObservable<ClientConnectionChange> ConnectionChanged => _connectionChanged.AsObservable();

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _connectionChanged?.OnCompleted();
        _connectionChanged.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _connectionChanged?.OnCompleted();
        _connectionChanged.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public void Start() => _acceptLoop ??= Task.Run(() => { return AcceptLoopAsync(_cts.Token); });

    public async Task StopAsync()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
        }

        if (_acceptLoop != null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        foreach (var kv in _clients)
        {
            await kv.Value.DisposeAsync().ConfigureAwait(false);
        }

        _clients.Clear();
    }

    public bool IsClientConnected(ClientId clientId) => _clients.ContainsKey(clientId);

    public IReadOnlyCollection<ClientId> ConnectedClientIds => _clients.Keys.ToArray();

    public async Task SendCoordinateAsync(ClientId clientId, double x, double y, CancellationToken ct = default)
    {
        if (_clients.TryGetValue(clientId, out var conn))
        {
            var msg = new ServerSendsCoordinateMessage { ClientId = clientId.Id, X = x, Y = y };
            await conn.SendAsync(msg, ct).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"Client {clientId} is not connected");
        }
    }

    public async Task SendCloseRequestAsync(ClientId clientId, CancellationToken ct = default)
    {
        if (_clients.TryGetValue(clientId, out var conn))
        {
            var msg = new ServerRequestsClientCloseMessage { ClientId = clientId.Id };
            await conn.SendAsync(msg, ct).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"Client {clientId} is not connected");
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? serverStream = null;
            try
            {
                serverStream = new NamedPipeServerStream(
                    PipeProtocol.DiscoveryPipeName,
                    PipeDirection.InOut,
                    10,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                await serverStream.WaitForConnectionAsync(ct).ConfigureAwait(false);

                _ = Task.Run(() => HandleClientAsync(serverStream, ct));
            }
            catch (OperationCanceledException)
            {
                serverStream?.Dispose();
                break;
            }
            catch
            {
                serverStream?.Dispose();
                // Brief back-off to avoid tight loop on repeated failures
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Handles communication with a connected client using a named pipe stream.
    /// </summary>
    /// <param name="stream">The named pipe stream used for communication with the client.</param>
    /// <param name="outerCt">A cancellation token to observe while waiting for operations to complete.</param>
    /// <returns>A task that represents the asynchronous operation of handling the client.</returns>
    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken outerCt)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        var ct = linkedCts.Token;

        var reader = new StreamReader(stream, Encoding.UTF8, false, leaveOpen: true);
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        ClientConnection? connection = null;
        ClientId? clientId = null;
        try
        {
            // Expect Hello first
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                return;
            }

            var msg = PipeSerialization.DeserializeForServer(line);
            if (msg is not ClientHelloMessage hello)
            {
                return; // unexpected, drop
            }

            clientId = new ClientId(hello.ClientId);

            connection =
                new ClientConnection(clientId, stream, reader, writer, () => RemoveClient(clientId));

            if (!_clients.TryAdd(clientId, connection))
            {
                // If already connected, replace it
                if (_clients.TryGetValue(clientId, out var existing))
                {
                    await existing.DisposeAsync().ConfigureAwait(false);
                }

                _clients[clientId] = connection;
            }

            _connectionChanged.OnNext(new ClientConnectionChange(clientId, ConnectionState.Connected));

            // Immediately send configuration to the client if a provider is available
            if (_configurationProvider is not null)
            {
                try
                {
                    var cfg = _configurationProvider.Value.BuildConfigurationFor(clientId);
                    if (connection is not null)
                    {
                        await connection.SendAsync(cfg, ct).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // ignore configuration send errors to avoid dropping the connection
                }
            }

            // Keep reading for polite bye or to detect disconnect
            while (!ct.IsCancellationRequested && stream.IsConnected)
            {
                var next = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (next is null)
                {
                    break;
                }

                var m = PipeSerialization.DeserializeForServer(next);
                if (m is ClientByeMessage)
                {
                    break;
                }
                // Server currently ignores other client->server messages
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
        catch
        {
            // ignore per-connection errors
        }
        finally
        {
            if (clientId is not null)
            {
                RemoveClient(clientId);
            }

            // Disposing stream is handled by ClientConnection; if connection not created, dispose now
            if (connection is null)
            {
                try
                {
                    stream.Dispose();
                }
                catch
                {
                }
            }
        }
    }

    private void RemoveClient(ClientId clientId)
    {
        if (_clients.TryRemove(clientId, out var conn))
        {
            try
            {
                conn.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch
            {
            }

            _connectionChanged.OnNext(new ClientConnectionChange(clientId, ConnectionState.Disconnected));
        }
    }

    /// <summary>
    ///     Represents an asynchronous client connection managed by the server in a named pipe communication context.
    /// </summary>
    /// <remarks>
    ///     This class handles the lifecycle of a client's connection, including sending messages to the client,
    ///     observing disconnections, and cleaning up resources when the client disconnects or is disposed.
    /// </remarks>
    /// <seealso cref="IAsyncDisposable" />
    private sealed class ClientConnection : IAsyncDisposable
    {
        private readonly ClientId _clientId;
        private readonly CancellationTokenSource _cts = new();
        private readonly Action _onDispose;

        private readonly Channel<IServerToClientMessage> _sendQueue =
            Channel.CreateUnbounded<IServerToClientMessage>(new UnboundedChannelOptions
                { SingleReader = true, SingleWriter = false });

        private readonly NamedPipeServerStream _stream;
        private readonly StreamWriter _writer;
        private readonly Task _writerLoop;

        public ClientConnection(ClientId clientId, NamedPipeServerStream stream, StreamReader reader,
            StreamWriter writer,
            Action onDispose)
        {
            _clientId = clientId;
            _stream = stream;
            _writer = writer;
            _onDispose = onDispose;
            _writerLoop = Task.Run(() => WriterLoopAsync(_cts.Token));
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _cts.Cancel();
            }
            catch
            {
            }

            try
            {
                _sendQueue.Writer.TryComplete();
            }
            catch
            {
            }

            try
            {
                await _writerLoop.ConfigureAwait(false);
            }
            catch
            {
            }

            try
            {
                _writer.Dispose();
            }
            catch
            {
            }

            try
            {
                _stream.Dispose();
            }
            catch
            {
            }

            try
            {
                _onDispose();
            }
            catch
            {
            }
        }

        public async Task SendAsync(IServerToClientMessage message, CancellationToken ct) =>
            await _sendQueue.Writer.WriteAsync(message, ct).ConfigureAwait(false);

        private async Task WriterLoopAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var msg in _sendQueue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    if (!_stream.IsConnected)
                    {
                        break;
                    }

                    var json = PipeSerialization.SerializeFromServer(msg);
                    await _writer.WriteLineAsync(json).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }
    }
}
