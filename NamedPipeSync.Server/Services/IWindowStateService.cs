using System.Threading;
using System.Threading.Tasks;

namespace NamedPipeSync.Server.Services;

/// <summary>
/// Presentation-layer service that minimizes and restores the current main window.
///
/// This service abstracts WPF window state changes behind a UI-owned API so that
/// view models can request state changes without directly depending on WPF types.
/// </summary>
public interface IWindowStateService
{
    /// <summary>
    /// Minimizes the current main window if available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token. If cancelled before the dispatcher operation runs, the task will complete in a cancelled state.</param>
    Task MinimizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the current main window to normal state and brings it to foreground if possible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token. If cancelled before the dispatcher operation runs, the task will complete in a cancelled state.</param>
    Task RestoreAsync(CancellationToken cancellationToken = default);
}
