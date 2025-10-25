using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Application;

/// <summary>
/// Application service responsible for managing client images.
/// Handles screenshot capture, image processing, and storage in domain entities.
/// </summary>
public interface IClientImageService
{
    /// <summary>
    /// Updates both current and preloaded images for a client based on ShowMode.
    /// Captures screenshot, applies ShowMode-specific transformation, and stores in domain entity.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="showMode">The current show mode determining image transformation.</param>
    Task UpdateClientImagesAsync(ClientId clientId, ShowMode showMode);
    
    /// <summary>
    /// Promotes the preloaded image to current for the specified client.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    void PromotePreloadedImage(ClientId clientId);
}
