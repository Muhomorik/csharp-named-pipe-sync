using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Application.Imaging;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Server.Services;

/// <summary>
/// Server-side implementation of client image management service.
/// Handles screenshot capture, image processing, and storage in domain entities.
/// </summary>
public sealed class ClientImageService : IClientImageService
{
    private readonly IClientWithRuntimeRepository _repository;
    private readonly IScreenCaptureService _screenCapture;
    private readonly IImageBase64Converter _converter;
    private readonly IImageProcessingService _processor;
    private readonly IWindowStateService _windowStateService;

    public ClientImageService(
        IClientWithRuntimeRepository repository,
        IScreenCaptureService screenCapture,
        IImageBase64Converter converter,
        IImageProcessingService processor,
        IWindowStateService windowStateService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _screenCapture = screenCapture ?? throw new ArgumentNullException(nameof(screenCapture));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
    }

    public async Task UpdateClientImagesAsync(ClientId clientId, ShowMode showMode)
    {
        if (!_repository.TryGet(clientId, out var client) || client == null)
            return;

        // Minimize window, capture screenshot, then restore
        await _windowStateService.MinimizeAsync().ConfigureAwait(false);
        var screenshotBytes = await _screenCapture.CaptureCurrentScreenPngAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await _windowStateService.RestoreAsync().ConfigureAwait(false);

        if (screenshotBytes.Length == 0)
        {
            // No screenshot captured, clear images
            client.SetCurrentImage(string.Empty);
            client.SetPreloadedImage(string.Empty);
            return;
        }

        var base64 = _converter.PngBytesToBase64(screenshotBytes);

        // Apply ShowMode-specific transformation
        var transformation = GetTransformationForShowMode(showMode);
        using var processed = _processor.ApplyTransformationFromBase64(base64, transformation);
        var processedBase64 = _converter.MagickImageToBase64Png(processed);

        // Update domain entity with both images
        // For now, both images are the same; this can be extended later
        client.SetCurrentImage(processedBase64);
        client.SetPreloadedImage(processedBase64);
    }

    public void PromotePreloadedImage(ClientId clientId)
    {
        if (_repository.TryGet(clientId, out var client))
        {
            client?.PromotePreloadedToCurrent();
        }
    }

    private static ImageTransformation GetTransformationForShowMode(ShowMode mode) => mode switch
    {
        ShowMode.Magic => ImageTransformation.Sepia,
        ShowMode.Debugging => ImageTransformation.None,
        ShowMode.NextVersion => ImageTransformation.None, // TBD
        _ => ImageTransformation.None
    };
}
