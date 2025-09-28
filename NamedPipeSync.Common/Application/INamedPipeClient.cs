using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Application;

/// <summary>
///     Defines the client-side contract for connecting to the Named Pipe server and receiving coordinates.
/// </summary>
/// <remarks>
///     - The client discovers the server via <see cref="PipeProtocol.DiscoveryPipeName" />.
///     - Notifications are pushed via IObservable on background threads; marshal to the UI thread if needed.
///     - Intended to be used per logical client id (typically 6 distinct clients in this app).
/// </remarks>
public interface INamedPipeClient : IAsyncDisposable, IDisposable
{
    /// <summary>
    ///     A unified stream that emits when the client connects or disconnects.
    /// </summary>
    /// <remarks>
    ///     - Emits on a background thread.
    ///     - Suitable for throttling/debouncing to avoid flakiness during rapid reconnecting.
    /// </remarks>
    IObservable<ClientConnectionStateChange> ConnectionChanged { get; }

    /// <summary>
    ///     Stream of coordinates destined for this client.
    /// </summary>
    IObservable<Coordinate> Coordinates { get; }

    /// <summary>
    ///     Attempts to discover and connect to the server, retrying until success or cancellation.
    /// </summary>
    /// <param name="retryDelay">
    ///     Optional delay between retry attempts. If <see langword="null" />, a sensible default (about
    ///     500 ms) is used.
    /// </param>
    /// <param name="ct">
    ///     A token that cancels the connecting loop. If canceled, an <see cref="OperationCanceledException" /> is
    ///     thrown.
    /// </param>
    /// <returns>A task that completes when the connection is established.</returns>
    /// <exception cref="OperationCanceledException">
    ///     Thrown if <paramref name="ct" /> is canceled before a connection is
    ///     established.
    /// </exception>
    Task ConnectAsync(TimeSpan? retryDelay = null, CancellationToken ct = default);

    /// <summary>
    ///     Sends a polite termination message (Bye) to the server when disconnecting.
    /// </summary>
    /// <param name="ct">Optional cancellation token to cancel the send operation.</param>
    /// <returns>A task representing the asynchronous sending.</returns>
    Task SendByeAsync(CancellationToken ct = default);

    /// <summary>
    ///     Terminates the connection and tears down all resources.
    /// </summary>
    /// <returns>A task that completes once the connection is closed and resources are released.</returns>
    Task DisconnectAsync();
}