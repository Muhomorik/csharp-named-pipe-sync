using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging;

/// <summary>
/// Magick.NET-based image processing service.
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

    public MagickImage Crop(MagickImage source, int x, int y, int width, int height)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        // Work on a clone to avoid mutating caller-owned image
        var clone = (MagickImage)source.Clone();

        // Clamp rectangle within bounds
        var rect = ClampRectangle(x, y, width, height, clone.Width, clone.Height);
        clone.Crop(rect);
        clone.RePage();

        return clone; // caller must Dispose
    }

    public MagickImage ApplySepiaToneToCropped(MagickImage source, int x, int y, int width, int height)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        var cropped = Crop(source, x, y, width, height);
        try
        {
            cropped.SepiaTone();
            return cropped; // caller must Dispose
        }
        catch
        {
            cropped.Dispose();
            throw;
        }
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
