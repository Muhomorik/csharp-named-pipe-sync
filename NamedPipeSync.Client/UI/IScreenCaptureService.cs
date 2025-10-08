using System.Threading;
using System.Threading.Tasks;

namespace NamedPipeSync.Client.UI;

/// <summary>
/// Presentation-layer service that captures a screenshot of the monitor where the current main window resides.
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Captures the entire screen (monitor) on which the application's main window is located as a PNG image.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token for the capture operation.</param>
    /// <returns>PNG-encoded image bytes of the monitor.</returns>
    Task<byte[]> CaptureCurrentScreenPngAsync(CancellationToken cancellationToken = default);
}
