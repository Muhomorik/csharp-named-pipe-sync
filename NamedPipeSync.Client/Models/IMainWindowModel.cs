using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;

namespace NamedPipeSync.Client.Models;

/// <summary>
/// Interface for MainWindow model providing encapsulated functionality
/// </summary>
public interface IMainWindowModel
{
    /// <summary>
    /// Gets the client identifier
    /// </summary>
    int GetClientId();

    /// <summary>
    /// Requests application shutdown
    /// </summary>
    void RequestShutdown();

    /// <summary>
    /// Connects to the named pipe server
    /// </summary>
    /// <param name="timeout">Connection timeout</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the connection operation</returns>
    Task ConnectAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the named pipe server
    /// </summary>
    /// <returns>Task representing the disconnection operation</returns>
    Task DisconnectAsync();

    /// <summary>
    /// Observable stream for connection state changes.
    /// Prefer exposing observables as read-only properties with noun names.
    /// </summary>
    IObservable<ClientConnectionStateChange> ConnectionChanges { get; }

    /// <summary>
    /// Observable stream for coordinate updates destined for this client.
    /// </summary>
    IObservable<Coordinate> Coordinates { get; }
}
