using ImageMagick;

namespace NamedPipeSync.Common.Application.Imaging.Transformations;

/// <summary>
/// Applies a blur effect to an image.
/// </summary>
public class BlurTransformation : IImageTransformationStrategy
{
    private readonly double _radius;
    private readonly double _sigma;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlurTransformation"/> class.
    /// </summary>
    /// <param name="radius">The radius of the blur effect. Default is 0 (auto-select).</param>
    /// <param name="sigma">The standard deviation of the blur. Default is 3.0.</param>
    public BlurTransformation(double radius = 0, double sigma = 3.0)
    {
        _radius = radius;
        _sigma = sigma;
    }

    /// <inheritdoc />
    public void Transform(MagickImage image)
    {
        image.Blur(_radius, _sigma);
    }
}
