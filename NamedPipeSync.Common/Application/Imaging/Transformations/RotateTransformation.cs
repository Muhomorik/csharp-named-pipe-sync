using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging.Transformations;

/// <summary>
/// Rotates an image by a specified angle.
/// </summary>
public class RotateTransformation : IImageTransformationStrategy
{
    private readonly double _degrees;

    /// <summary>
    /// Initializes a new instance of the <see cref="RotateTransformation"/> class.
    /// </summary>
    /// <param name="degrees">The angle in degrees to rotate the image. Default is 90 degrees.</param>
    public RotateTransformation(double degrees = 90)
    {
        _degrees = degrees;
    }

    /// <inheritdoc />
    public void Transform(MagickImage image)
    {
        image.Rotate(_degrees);
    }
}
