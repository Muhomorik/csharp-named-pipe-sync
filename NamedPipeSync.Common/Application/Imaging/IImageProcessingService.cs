using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NamedPipeSync.Common.Application.Imaging;

/// <summary>
/// Image processing application service that encapsulates common image operations used by both Server and Client.
/// The service hides concrete imaging library types from consumers (e.g., Magick.NET) and exposes simple byte/base64 APIs.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Saves a Base64-encoded PNG image to the specified directory.
    /// </summary>
    /// <param name="base64Png">Base64 string representing a PNG image. May be empty; empty does nothing and returns an empty path.</param>
    /// <param name="directory">Target directory. Will be created if it does not exist.</param>
    /// <param name="fileName">Optional file name without directory. If null, a timestamp-based name will be used.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the write.</param>
    /// <returns>Full path to the saved file, or empty string if <paramref name="base64Png"/> is null/empty.</returns>
    Task<string> SaveBase64PngAsync(string? base64Png, string directory, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crops the PNG supplied as Base64 at the specified rectangle and returns a PNG as raw bytes.
    /// Coordinates are in pixels. Out-of-range values will be clamped to the image bounds.
    /// </summary>
    /// <param name="base64Png">Source image (PNG) as Base64.</param>
    /// <param name="x">Left X coordinate in pixels (0-based).</param>
    /// <param name="y">Top Y coordinate in pixels (0-based).</param>
    /// <param name="width">Width in pixels; must be > 0.</param>
    /// <param name="height">Height in pixels; must be > 0.</param>
    /// <returns>PNG bytes for the cropped region.</returns>
    byte[] CropPngFromBase64(string base64Png, int x, int y, int width, int height);

    /// <summary>
    /// Crops the PNG supplied as Base64 then applies a SepiaTone effect to the cropped image and returns PNG bytes.
    /// </summary>
    /// <param name="base64Png">Source image (PNG) as Base64.</param>
    /// <param name="x">Left X coordinate in pixels (0-based).</param>
    /// <param name="y">Top Y coordinate in pixels (0-based).</param>
    /// <param name="width">Width in pixels; must be > 0.</param>
    /// <param name="height">Height in pixels; must be > 0.</param>
    /// <returns>PNG bytes of the cropped image with SepiaTone applied.</returns>
    byte[] ApplySepiaToneToCroppedBase64(string base64Png, int x, int y, int width, int height);
}
