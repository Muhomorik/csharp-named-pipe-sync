using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging;

/// <summary>
/// Magick.NET-based image processing service. Keeps Magick types internal to the implementation.
/// </summary>
public sealed class ImageProcessingService : IImageProcessingService
{
    public async Task<string> SaveBase64PngAsync(string? base64Png, string directory, string? fileName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(base64Png))
            return string.Empty;
        if (directory is null) throw new ArgumentNullException(nameof(directory));

        Directory.CreateDirectory(directory);
        var name = string.IsNullOrWhiteSpace(fileName)
            ? $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png"
            : fileName;
        var fullPath = Path.Combine(directory, name);

        var bytes = Convert.FromBase64String(base64Png);
        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
#if NETSTANDARD2_0
        await fs.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
#else
        await fs.WriteAsync(bytes, cancellationToken);
#endif
        await fs.FlushAsync(cancellationToken);
        return fullPath;
    }

    public byte[] CropPngFromBase64(string base64Png, int x, int y, int width, int height)
    {
        if (base64Png is null) throw new ArgumentNullException(nameof(base64Png));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        var bytes = Convert.FromBase64String(base64Png);
        using var image = new MagickImage(bytes);

        // Clamp rectangle within bounds
        var rect = ClampRectangle(x, y, width, height, image.Width, image.Height);
        image.Crop(rect);
        image.RePage();

        return image.ToByteArray(MagickFormat.Png);
    }

    public byte[] ApplySepiaToneToCroppedBase64(string base64Png, int x, int y, int width, int height)
    {
        if (base64Png is null) throw new ArgumentNullException(nameof(base64Png));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        var bytes = Convert.FromBase64String(base64Png);
        using var image = new MagickImage(bytes);

        var rect = ClampRectangle(x, y, width, height, image.Width, image.Height);
        image.Crop(rect);
        image.RePage();

        image.SepiaTone();
        return image.ToByteArray(MagickFormat.Png);
    }

    private static MagickGeometry ClampRectangle(int x, int y, int width, int height, int maxWidth, int maxHeight)
    {
        var clampedX = Math.Clamp(x, 0, Math.Max(0, maxWidth - 1));
        var clampedY = Math.Clamp(y, 0, Math.Max(0, maxHeight - 1));
        var clampedW = Math.Clamp(width, 1, Math.Max(1, maxWidth - clampedX));
        var clampedH = Math.Clamp(height, 1, Math.Max(1, maxHeight - clampedY));
        return new MagickGeometry(clampedX, clampedY, clampedW, clampedH);
    }
}
