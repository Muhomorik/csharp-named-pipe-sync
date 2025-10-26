using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging.Transformations;

/// <summary>
/// Automatically adjusts image levels for optimal contrast.
/// </summary>
public class AutoLevelTransformation : IImageTransformationStrategy
{
    /// <inheritdoc />
    public void Transform(MagickImage image)
    {
        image.AutoLevel();
    }
}
