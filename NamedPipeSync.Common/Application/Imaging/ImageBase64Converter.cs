using System;
using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging;

/// <summary>
/// Provides helper methods to convert images to and from Base64 strings for transport over the wire.
/// Intended for scenarios where the server sends PNG-encoded images to clients and the client renders them.
/// </summary>
public sealed class ImageBase64Converter : IImageBase64Converter
{
    /// <summary>
    /// Encodes a WPF <see cref="BitmapSource"/> into PNG format and returns a Base64 string.
    /// </summary>
    /// <param name="bitmap">The source bitmap to encode. Must not be null and must be freezable or accessible on the calling thread.</param>
    /// <returns>A Base64 string representing the PNG-encoded image. Never null; may be empty only if the image has zero bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="bitmap"/> is null.</exception>
    public string BitmapSourceToBase64Png(BitmapSource bitmap)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Converts raw PNG bytes to a Base64 string. Useful on the server when a PNG file is already available as bytes.
    /// </summary>
    /// <param name="pngBytes">Raw PNG data. Must not be null.</param>
    /// <returns>Base64 string for the provided PNG bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="pngBytes"/> is null.</exception>
    public string PngBytesToBase64(byte[] pngBytes)
    {
        if (pngBytes is null) throw new ArgumentNullException(nameof(pngBytes));
        return Convert.ToBase64String(pngBytes);
    }

    /// <summary>
    /// Encodes an ImageMagick <see cref="MagickImage"/> into PNG format and returns a Base64 string.
    /// </summary>
    /// <param name="image">The source Magick image to encode. Must not be null.</param>
    /// <returns>A Base64 string representing the PNG-encoded image.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="image"/> is null.</exception>
    public string MagickImageToBase64Png(MagickImage image)
    {
        if (image is null) throw new ArgumentNullException(nameof(image));
        var pngBytes = image.ToByteArray(MagickFormat.Png);
        return Convert.ToBase64String(pngBytes);
    }

    /// <summary>
    /// Decodes a Base64 string (PNG-encoded image) to a frozen <see cref="WriteableBitmap"/> ready for WPF rendering.
    /// </summary>
    /// <param name="base64">Base64 string containing a PNG image. Must not be null or empty.</param>
    /// <returns>A frozen <see cref="WriteableBitmap"/> that can be assigned to Image.Source or drawn onto.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="base64"/> is null.</exception>
    /// <exception cref="FormatException">Thrown if <paramref name="base64"/> is not a valid Base64 string.</exception>
    public WriteableBitmap Base64ToWriteableBitmap(string base64)
    {
        if (base64 is null) throw new ArgumentNullException(nameof(base64));
        if (string.IsNullOrWhiteSpace(base64))
        {
            // Create a 1x1 transparent bitmap as a safe fallback for null/empty/whitespace input
            var emptyWb = new WriteableBitmap(1, 1, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
            emptyWb.Freeze();
            return emptyWb;
        }

        var bytes = Convert.FromBase64String(base64);
        using var ms = new MemoryStream(bytes);

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad; // load into memory so we can dispose stream
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();

        var resultWb = new WriteableBitmap(bmp);
        resultWb.Freeze();
        return resultWb;
    }

    /// <summary>
    /// Applies an image <see cref="ImageTransformation"/> to the provided Base64-encoded image and returns a frozen <see cref="WriteableBitmap"/> suitable for WPF binding.
    /// </summary>
    /// <param name="base64Image">Base64 string representing the source image. May be null/empty; in that case a 288x288 solid dark bitmap (BGRA 37,37,37,255) is returned.</param>
    /// <param name="transformation">Transformation to apply. See <see cref="ImageTransformation"/>.</param>
    /// <returns>
    /// A frozen <see cref="WriteableBitmap"/> with the transformation applied. Never null.
    /// The returned bitmap is already Frozen and does not need to be disposed or wrapped in a using statement.
    /// All temporary native resources (e.g., MagickImage) are disposed within this method.
    /// </returns>
    public WriteableBitmap GetProcessedImage(string? base64Image, ImageTransformation transformation)
    {
        // Empty input: return a 288x288 solid dark bitmap (BGRA 37,37,37,255)
        if (string.IsNullOrWhiteSpace(base64Image))
        {
            const int width = 288;
            const int height = 288;
            var fallbackWb = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);

            // Create pixel buffer for solid color (BGRA)
            var pixels = new byte[width * height * 4];
            for (var i = 0; i < width * height; i++)
            {
                pixels[i * 4 + 0] = 37;  // B
                pixels[i * 4 + 1] = 37;  // G
                pixels[i * 4 + 2] = 37;  // R
                pixels[i * 4 + 3] = 255; // A
            }

            fallbackWb.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, 4 * width, 0);
            fallbackWb.Freeze();
            return fallbackWb;
        }

        // Use the existing processing service to apply transformation
        var processing = new ImageProcessingService();
        using var processed = processing.ApplyTransformationFromBase64(base64Image, transformation);
        var processedBase64 = MagickImageToBase64Png(processed);
        var wb = Base64ToWriteableBitmap(processedBase64);
        wb.Freeze();
        return wb;
    }
}
