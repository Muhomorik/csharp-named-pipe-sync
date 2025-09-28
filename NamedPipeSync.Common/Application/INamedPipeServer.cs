using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Application;

/// <summary>
///     Defines the server-side contract for the application's Named Pipe based IPC layer.
///     <para>
///         An implementation accepts multiple client connections over a well-known discovery pipe name
///         (see <see cref="PipeProtocol.DiscoveryPipeName" />), tracks client connection state, and
///         allows sending coordinate payloads (X,Y) to a specific client by its numeric identifier.
///     </para>
///     <para>
///         This interface abstracts the concrete server so that UI, services, and automated agents can
///         depend on a stable API without pulling the implementation into memory. It intentionally exposes
///         only the operations and notifications needed by the application domain.
///     </para>
/// </summary>
/// <remarks>
///     Lifecycle and threading:
///     <list type="bullet">
///         <item>
///             <description>
///                 Call <see cref="Start" /> once to begin accepting connections. The server continues running
///                 until <see cref="StopAsync" /> is invoked or the instance is disposed.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Connection change notifications are pushed via IObservable on a thread-pool thread; subscribers must
///                 marshal back to the UI thread if necessary.
///             </description>
///         </item>
///         <item>
///             <description>
///                 The implementation is expected to be safe to call from multiple threads. Individual method
///                 calls are thread-safe unless noted otherwise.
///             </description>
///         </item>
///     </list>
///     Client identity and scope:
///     <list type="bullet">
///         <item>
///             <description>
///                 Clients are identified by a <see cref="ClientId" />. If a duplicate client connects with the
///                 same id, the previous connection may be replaced at the server's discretion.
///             </description>
///         </item>
///     </list>
/// </remarks>
public interface INamedPipeServer : IAsyncDisposable, IDisposable
{
    /// <summary>
    ///     Gets the snapshot of client identifiers that are currently connected.
    /// </summary>
    /// <remarks>
    ///     The returned collection is an immutable snapshot. It may be empty if no clients are connected.
    /// </remarks>
    IReadOnlyCollection<ClientId> ConnectedClientIds { get; }

    /// <summary>
    ///     A unified stream that emits a change whenever a client connects or disconnects.
    /// </summary>
    /// <remarks>
    ///     - Emits on a background thread.
    ///     - Suitable for throttling/debouncing to avoid flakiness during rapid reconnects.
    /// </remarks>
    IObservable<ClientConnectionChange> ConnectionChanged { get; }

    /// <summary>
    ///     Starts the server accept loop if it is not already running.
    /// </summary>
    /// <remarks>
    ///     This method is idempotent: calling it multiple times has no additional effect after the first start.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has already been disposed.</exception>
    void Start();

    /// <summary>
    ///     Stops the server, cancels accept/connection loops, and disposes all active client connections.
    /// </summary>
    /// <returns>A task that completes once all server activities have shut down.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has already been disposed.</exception>
    Task StopAsync();

    /// <summary>
    ///     Checks whether a client with the specified id is currently connected.
    /// </summary>
    /// <param name="clientId">
    ///     The client identifier. Must be within the application's valid range (e.g., 0..5 or
    ///     1..6).
    /// </param>
    /// <returns><see langword="true" /> if the client is connected; otherwise <see langword="false" />.</returns>
    bool IsClientConnected(ClientId clientId);

    /// <summary>
    ///     Sends a coordinate message to the specified client.
    /// </summary>
    /// <param name="clientId">The target client's identifier. The client must be connected.</param>
    /// <param name="x">The X coordinate value.</param>
    /// <param name="y">The Y coordinate value.</param>
    /// <param name="ct">
    ///     An optional cancellation token that cancels the send operation. Passing <see cref="CancellationToken.None" />
    ///     means the operation will only be canceled by server shutdown.
    /// </param>
    /// <returns>A task that completes when the message has been enqueued or written to the client connection.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the target client is not connected.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the server has been disposed.</exception>
    Task SendCoordinateAsync(ClientId clientId, double x, double y, CancellationToken ct = default);

    /// <summary>
    ///     Requests the specified client to close itself gracefully.
    /// </summary>
    /// <param name="clientId">The target client identifier.</param>
    /// <param name="ct">Cancellation token for the send operation.</param>
    Task SendCloseRequestAsync(ClientId clientId, CancellationToken ct = default);
}