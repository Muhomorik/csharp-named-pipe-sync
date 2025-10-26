using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging.Transformations;

/// <summary>
/// Converts an image to grayscale.
/// </summary>
public class GrayscaleTransformation : IImageTransformationStrategy
{
    /// <inheritdoc />
    public void Transform(MagickImage image)
    {
        image.Grayscale();
    }
}
