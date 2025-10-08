using System.Threading;
using System.Threading.Tasks;

namespace NamedPipeSync.Client.UI;

/// <summary>
/// Presentation-layer service that minimizes and restores the current main window.
/// </summary>
public interface IWindowStateService
{
    /// <summary>
    /// Minimizes the current main window if available.
    /// </summary>
    Task MinimizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the current main window to normal state and brings it to foreground if possible.
    /// </summary>
    Task RestoreAsync(CancellationToken cancellationToken = default);
}
