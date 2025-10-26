namespace NamedPipeSync.Common.Application.Imaging;

/// <summary>
/// Enumerates supported image transformations.
/// </summary>
public enum ImageTransformation
{
    /// <summary>
    /// No transformation applied; returns the original image.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Apply a Sepia tone effect to the image.
    /// </summary>
    Sepia = 1,
    
    /// <summary>
    /// Rotate the image by a specified angle.
    /// </summary>
    Rotate = 2,
    
    /// <summary>
    /// Convert the image to grayscale.
    /// </summary>
    Grayscale = 3,
    
    /// <summary>
    /// Invert the colors of the image (negative effect).
    /// </summary>
    Negative = 4,
    
    /// <summary>
    /// Apply a blur effect to the image.
    /// </summary>
    Blur = 5,
    
    /// <summary>
    /// Sharpen the image.
    /// </summary>
    Sharpen = 6,
    
    /// <summary>
    /// Automatically adjust image levels for optimal contrast.
    /// </summary>
    AutoLevel = 7,
}
