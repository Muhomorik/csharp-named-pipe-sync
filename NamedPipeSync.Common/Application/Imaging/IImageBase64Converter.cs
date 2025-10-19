using System.Windows.Media.Imaging;
using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging;

/// <summary>
/// Defines methods to convert images and byte arrays to and from Base64 strings.
/// This abstraction enables dependency injection and unit testing without static helpers.
/// </summary>
public interface IImageBase64Converter
{
    /// <summary>
    /// Encodes a WPF <see cref="BitmapSource"/> into PNG format and returns a Base64 string.
    /// </summary>
    /// <param name="bitmap">The source bitmap to encode. Must not be null and must be accessible on the calling thread.</param>
    /// <returns>A Base64 string representing the PNG-encoded image. Never null.</returns>
    string BitmapSourceToBase64Png(BitmapSource bitmap);

    /// <summary>
    /// Converts raw PNG bytes to a Base64 string.
    /// </summary>
    /// <param name="pngBytes">Raw PNG data. Must not be null.</param>
    /// <returns>Base64 string for the provided PNG bytes.</returns>
    string PngBytesToBase64(byte[] pngBytes);

    /// <summary>
    /// Encodes an ImageMagick <see cref="MagickImage"/> into PNG format and returns a Base64 string.
    /// </summary>
    /// <param name="image">The source Magick image to encode. Must not be null.</param>
    /// <returns>A Base64 string representing the PNG-encoded image.</returns>
    string MagickImageToBase64Png(MagickImage image);

    /// <summary>
    /// Decodes a Base64 string (PNG-encoded image) to a frozen <see cref="WriteableBitmap"/> ready for WPF rendering.
    /// </summary>
    /// <param name="base64">Base64 string containing a PNG image. May be empty; empty produces a 1x1 transparent bitmap.</param>
    /// <returns>A frozen <see cref="WriteableBitmap"/>.</returns>
    WriteableBitmap Base64ToWriteableBitmap(string base64);

    /// <summary>
    /// Applies the specified transformation to a Base64-encoded image and returns a frozen WriteableBitmap.
    /// This composes image processing and conversion into a single convenience call without introducing DI coupling.
    /// </summary>
    /// <param name="processingService">Processing service used to apply the transformation. Must not be null.</param>
    /// <param name="base64Image">Base64 source image. If null/empty, a fallback bitmap is returned.</param>
    /// <param name="transformation">Transformation to apply.</param>
    /// <returns>A frozen WriteableBitmap ready for WPF binding.</returns>
    WriteableBitmap TransformBase64ToWriteableBitmap(IImageProcessingService processingService, string? base64Image, ImageTransformation transformation);
}
