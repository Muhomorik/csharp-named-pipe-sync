using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging;

/// <summary>
/// Defines a strategy for transforming images.
/// </summary>
public interface IImageTransformationStrategy
{
    /// <summary>
    /// Transforms the specified image.
    /// </summary>
    /// <param name="image">The image to transform.</param>
    void Transform(MagickImage image);
}
