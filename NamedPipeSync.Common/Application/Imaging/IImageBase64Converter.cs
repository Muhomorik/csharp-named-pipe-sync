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
    /// Applies an image <see cref="ImageTransformation"/> to the provided Base64-encoded image and returns a frozen <see cref="WriteableBitmap"/> suitable for WPF binding.
    /// This convenience method encapsulates decoding, processing, and re-encoding steps into a single call for Presentation layer usage.
    /// </summary>
    /// <param name="base64Image">Base64 string representing the source image. May be null/empty; in that case a 1x1 transparent bitmap is returned.</param>
    /// <param name="transformation">Transformation to apply. See <see cref="ImageTransformation"/>.</param>
    /// <returns>
    /// A frozen <see cref="WriteableBitmap"/> with the transformation applied. Never null.
    /// The returned bitmap is already Frozen and does not need to be disposed or wrapped in a using statement.
    /// All temporary native resources (e.g., <see cref="ImageMagick.MagickImage"/>) are disposed within this method.
    /// </returns>
    WriteableBitmap GetProcessedImage(string? base64Image, ImageTransformation transformation);
}
