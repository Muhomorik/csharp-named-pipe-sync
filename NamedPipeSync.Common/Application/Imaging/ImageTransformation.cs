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
}
