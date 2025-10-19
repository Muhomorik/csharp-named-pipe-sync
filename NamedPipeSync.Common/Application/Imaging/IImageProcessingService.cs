using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging;

/// <summary>
/// Image processing application service that encapsulates common image operations used by both Server and Client.
/// NOTE: These APIs exchange MagickImage to avoid unnecessary byte[] conversions. Callers own the lifetime of returned images and must dispose them.
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
    /// Applies a transformation to an image provided as a Base64-encoded string and returns a new MagickImage.
    /// The caller owns and must dispose the returned image.
    /// </summary>
    /// <param name="base64Image">Base64 string representing the source image. If null/empty, an empty image will be used.</param>
    /// <param name="transformation">Transformation to apply. See <see cref="ImageTransformation"/>.</param>
    /// <returns>A new MagickImage with the transformation applied.</returns>
    MagickImage ApplyTransformationFromBase64(string? base64Image, ImageTransformation transformation);

    /// <summary>
    /// Crops the provided image at the specified rectangle and returns a new MagickImage.
    /// Coordinates are in pixels. Out-of-range values will be clamped to the image bounds.
    /// </summary>
    /// <param name="source">Source image. Must not be null. The method does not mutate this instance.</param>
    /// <param name="x">Left X coordinate in pixels (0-based).</param>
    /// <param name="y">Top Y coordinate in pixels (0-based).</param>
    /// <param name="width">Width in pixels; must be > 0.</param>
    /// <param name="height">Height in pixels; must be > 0.</param>
    /// <returns>A new MagickImage representing the cropped region. Caller owns and must dispose.</returns>
    MagickImage Crop(MagickImage source, int x, int y, int width, int height);

    /// <summary>
    /// Crops the provided image and applies a SepiaTone effect to the cropped image.
    /// </summary>
    /// <param name="source">Source image. Must not be null. The method does not mutate this instance.</param>
    /// <param name="x">Left X coordinate in pixels (0-based).</param>
    /// <param name="y">Top Y coordinate in pixels (0-based).</param>
    /// <param name="width">Width in pixels; must be > 0.</param>
    /// <param name="height">Height in pixels; must be > 0.</param>
    /// <returns>A new MagickImage with SepiaTone applied. Caller owns and must dispose.</returns>
    MagickImage ApplySepiaToneToCropped(MagickImage source, int x, int y, int width, int height);
}
