using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging.Transformations;

/// <summary>
/// Inverts the colors of an image (negative effect).
/// </summary>
public class NegativeTransformation : IImageTransformationStrategy
{
    /// <inheritdoc />
    public void Transform(MagickImage image)
    {
        image.Negate();
    }
}
