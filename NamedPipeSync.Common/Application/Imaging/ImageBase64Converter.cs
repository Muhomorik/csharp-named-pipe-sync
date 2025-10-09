using System;
using System.IO;
using System.Windows.Media.Imaging;

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
}
