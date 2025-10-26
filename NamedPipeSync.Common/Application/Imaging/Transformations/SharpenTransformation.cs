using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging.Transformations;

/// <summary>
/// Sharpens an image.
/// </summary>
public class SharpenTransformation : IImageTransformationStrategy
{
    private readonly double _radius;
    private readonly double _sigma;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharpenTransformation"/> class.
    /// </summary>
    /// <param name="radius">The radius of the sharpen effect. Default is 0 (auto-select).</param>
    /// <param name="sigma">The standard deviation of the sharpen. Default is 1.0.</param>
    public SharpenTransformation(double radius = 0, double sigma = 1.0)
    {
        _radius = radius;
        _sigma = sigma;
    }

    /// <inheritdoc />
    public void Transform(MagickImage image)
    {
        image.Sharpen(_radius, _sigma);
    }
}
