using System;
using System.Threading;
using System.Threading.Tasks;

namespace NamedPipeSync.Common.Application;

/// <summary>
/// Orchestrates periodic coordinate calculation and sending to clients.
/// This is an Application-layer scheduler that owns a timer and exposes operations to
/// start/stop the loop, reset positions, and change the interval. It does not interact
/// with UI or process lifetime APIs.
/// </summary>
public interface ICoordinatesSendScheduler
{
    /// <summary>
    /// Starts periodic sending if not already started. Idempotent.
    /// </summary>
    void StartSending();

    /// <summary>
    /// Stops periodic sending. Safe to call multiple times.
    /// </summary>
    void StopSending();

    /// <summary>
    /// Resets each client's position to its last checkpoint and marks it as being on a checkpoint.
    /// Implementations should send the reset coordinates to connected clients and persist the updated state.
    /// </summary>
    /// <param name="ct">Cancellation token. If canceled, the operation should throw <see cref="OperationCanceledException"/>.</param>
    Task ResetPositionAsync(CancellationToken ct = default);

    /// <summary>
    /// Changes the periodic timer interval. If the scheduler is currently running, it should restart with the new interval.
    /// </summary>
    /// <param name="interval">New interval; must be greater than <see cref="TimeSpan.Zero"/>.</param>
    void ChangeTimer(TimeSpan interval);
}
