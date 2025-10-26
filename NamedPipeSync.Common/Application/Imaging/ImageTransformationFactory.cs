using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using NamedPipeSync.Common.Application.Imaging.Transformations;

namespace NamedPipeSync.Common.Application.Imaging;

/// <summary>
/// Factory for creating and applying image transformations.
/// </summary>
public class ImageTransformationFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTransformationFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving transformation strategies.</param>
    public ImageTransformationFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Applies the specified transformation to the image.
    /// </summary>
    /// <param name="transformation">The type of transformation to apply.</param>
    /// <param name="image">The image to transform.</param>
    /// <returns>The transformed image.</returns>
    /// <exception cref="ArgumentException">Thrown when an unsupported transformation type is specified.</exception>
    public MagickImage ApplyTransformation(ImageTransformation transformation, MagickImage image)
    {
        if (transformation == ImageTransformation.None)
        {
            return image;
        }

        var strategy = GetTransformationStrategy(transformation);
        strategy.Transform(image);
        return image;
    }

    private IImageTransformationStrategy GetTransformationStrategy(ImageTransformation transformation)
    {
        return transformation switch
        {
            ImageTransformation.Rotate => _serviceProvider.GetRequiredService<RotateTransformation>(),
            ImageTransformation.Grayscale => _serviceProvider.GetRequiredService<GrayscaleTransformation>(),
            ImageTransformation.Negative => _serviceProvider.GetRequiredService<NegativeTransformation>(),
            ImageTransformation.Blur => _serviceProvider.GetRequiredService<BlurTransformation>(),
            ImageTransformation.Sharpen => _serviceProvider.GetRequiredService<SharpenTransformation>(),
            ImageTransformation.AutoLevel => _serviceProvider.GetRequiredService<AutoLevelTransformation>(),
            _ => throw new ArgumentException($"Unsupported transformation type: {transformation}", nameof(transformation))
        };
    }
}
