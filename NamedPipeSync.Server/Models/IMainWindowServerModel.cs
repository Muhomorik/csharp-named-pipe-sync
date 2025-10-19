using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Domain.Events;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Server.Models;

/// <summary>
/// SERVER. Model abstraction for the server MainWindow.
/// 
/// Purpose
/// - Provides a UI-free orchestration seam between the Presentation layer (WPF) and lower-level services.
/// - Exposes observable intent streams and application-level operations consumed by the ViewModel.
/// 
/// Important constraints for implementers
/// - Must not perform process termination or interact with UI/Dispatcher APIs.
/// - Should be side-effect free with respect to UI; only expose data/intent signals.
/// - Follow async best practices; do not block calling threads.
/// 
/// Relationships
/// - Consumed by MainWindowServerViewModel in the Server application.
/// - Composes Infrastructure/Application services (e.g., pipe server, repositories, calculators) but hides them from the ViewModel.
/// </summary>
public interface IMainWindowServerModel
{
    /// <summary>
    /// Observable stream of client connection state changes emitted by the server transport.
    /// Presentation subscribes and updates UI state accordingly.
    /// </summary>
    /// <remarks>
    /// - This stream is UI-agnostic; consumers should switch to the UI scheduler before mutating UI-bound state.
    /// - Errors in the underlying transport should be surfaced as OnError.
    /// </remarks>
    IObservable<ClientConnectionChange> ConnectionChanged { get; }

    /// <summary>
    /// Observable stream of domain/runtime events related to clients (e.g., coordinates updates, checkpoints).
    /// </summary>
    /// <remarks>
    /// - Events are emitted as immutable messages; consumers compose by type (e.g., OfType&lt;CoordinatesUpdated&gt;()).
    /// - UI mutations must be scheduled on the UI thread by the consumer.
    /// </remarks>
    IObservable<ClientWithRuntimeEvent> Events { get; }

    /// <summary>
    /// Starts the server transport and any necessary background orchestration.
    /// </summary>
    /// <remarks>
    /// - Non-blocking: implementations should return immediately after initiating startup.
    /// - Must not interact with UI or process lifetime.
    /// </remarks>
    void StartServer();

    /// <summary>
    /// Idempotently seeds missing client entries in the runtime repository based on available start checkpoints.
    /// </summary>
    /// <remarks>
    /// - Safe to call multiple times; existing entries are preserved.
    /// - No UI/threading requirements.
    /// </remarks>
    void SeedMissingClients();

    /// <summary>
    /// Ensures a repository entry exists for the specified client and updates its connection state.
    /// </summary>
    /// <param name="clientId">Logical client identifier. Must be a valid, non-default id.</param>
    /// <param name="state">New connection state to store.</param>
    /// <remarks>
    /// - If the client entry is missing, an entry is created using a reasonable default checkpoint.
    /// - Thread-safe usage is expected from consumers; implementations should avoid UI dependencies.
    /// </remarks>
    void EnsureClientEntryOnConnectionChange(ClientId clientId, ConnectionState state);

    /// <summary>
    /// Returns a snapshot of all known clients with their current runtime state.
    /// </summary>
    /// <returns>An immutable snapshot collection; never null.</returns>
    IReadOnlyCollection<ClientWithRuntime> GetAllClients();

    /// <summary>
    /// Tries to get the current runtime state for the specified client.
    /// </summary>
    /// <param name="clientId">The client identifier to lookup.</param>
    /// <param name="client">When found, receives the associated client state; otherwise receives null.</param>
    /// <returns>True if the client exists in the repository; otherwise false.</returns>
    bool TryGet(ClientId clientId, out ClientWithRuntime? client);

    /// <summary>
    /// Starts a single client process corresponding to the given id.
    /// </summary>
    /// <param name="id">Client identifier whose process should be started.</param>
    /// <remarks>
    /// - Fire-and-forget; implementations should not block the UI thread.
    /// - Caller is responsible for reflecting connection state via observables.
    /// </remarks>
    void StartClient(ClientId id);

    /// <summary>
    /// Starts multiple client processes.
    /// </summary>
    /// <param name="ids">Sequence of client ids to start. Must not be null.</param>
    /// <param name="ct">Cancellation token. If canceled, the operation should stop starting additional clients and throw OperationCanceledException.</param>
    /// <returns>A task that completes when the start attempts finish. The task should not capture the synchronization context.</returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="ct"/> is canceled before or during the operation.</exception>
    Task StartManyAsync(IEnumerable<ClientId> ids, CancellationToken ct = default);

    /// <summary>
    /// Begins periodic coordinate calculation and sending to connected clients. Idempotent.
    /// </summary>
    void StartSending();

    /// <summary>
    /// Stops periodic coordinate sending. Safe to call multiple times.
    /// </summary>
    void StopSending();

    /// <summary>
    /// Resets each client's position to its last checkpoint and sends an update.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task ResetPositionAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a close/shutdown request to all currently connected clients.
    /// </summary>
    /// <param name="ct">Cancellation token. If canceled, pending close requests should be canceled and the returned task should observe the cancellation.</param>
    /// <returns>A task representing the asynchronous close broadcast. The task should not capture the synchronization context.</returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="ct"/> is canceled.</exception>
    Task CloseAllClientsAsync(CancellationToken ct = default);

    /// <summary>
    /// Current server-wide show mode preset. Changing this value should not produce UI side effects here; it is an application-level state.
    /// </summary>
    ShowMode CurrentShowMode { get; set; }

    /// <summary>
    /// Returns a snapshot of the currently connected client identifiers from the server transport.
    /// UI should not infer connection state from UI-bound collections; use this authoritative source instead.
    /// </summary>
    IReadOnlyCollection<ClientId> GetCurrentlyConnectedClientIds();

    /// <summary>
    /// Processes a captured screenshot by converting PNG bytes to Base64 and restarts the specified clients.
    /// </summary>
    /// <param name="pngBytes">Raw PNG bytes captured from the screen. May be null or empty; null/empty results in an empty string.</param>
    /// <param name="reconnectIds">Client ids to restart after processing the screenshot. May be empty.</param>
    /// <param name="ct">Cancellation token to cancel the restart operation.</param>
    /// <returns>The Base64 PNG string produced from the provided bytes. Never null (empty string on null/empty input).</returns>
    Task<string> ProcessScreenshotAndRestartAsync(byte[] pngBytes, IEnumerable<ClientId> reconnectIds, CancellationToken ct = default);

    /// <summary>
    /// Orchestrates the full capture-and-restart workflow: determines connected clients, closes all clients,
    /// minimizes the window, captures the current screen as PNG bytes, restores the window, and then processes
    /// the screenshot and restarts the previously connected clients.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation cooperatively.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CaptureScreenAndRestartClientsAsync(CancellationToken ct = default);
}
