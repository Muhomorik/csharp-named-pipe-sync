using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;

using DevExpress.Mvvm;

using JetBrains.Annotations;

using NamedPipeSync.Client.Models;
using NamedPipeSync.Common.Application;

using NLog;

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NamedPipeSync.Client.ViewModels;

/// <summary>
///     CLIENT. ViewModel for the main window.
/// </summary>
public class MainWindowClientViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger _logger;
    private readonly IMainWindowModel _model;
    private readonly IScheduler _uiScheduler;

    private readonly CompositeDisposable _disposables = new();
    private string _title = "VM: Client Window";

    // Background image bound from the ViewModel to the View (explicit WriteableBitmap)
    private WriteableBitmap _backgroundImage;

    public WriteableBitmap BackgroundImage
    {
        get => _backgroundImage;
        set => SetProperty(ref _backgroundImage, value, nameof(BackgroundImage));
    }

    /// <summary>
    /// Creates a solid background image with predefined dimensions and color.
    /// </summary>
    /// <returns>A frozen <see cref="WriteableBitmap"/> instance representing the background image.</returns>
    private WriteableBitmap CreateBackgroundImage()
    {
        const int width = 288;
        const int height = 288;
        var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);

        // Create a pixel array for a solid color (BGRA: 37, 37, 37, 255)
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            pixels[i * 4 + 0] = 37; // Blue
            pixels[i * 4 + 1] = 37; // Green
            pixels[i * 4 + 2] = 37; // Red
            pixels[i * 4 + 3] = 255; // Alpha
        }

        wb.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, 4 * width, 0);
        wb.Freeze();
        return wb;
    }

    /// <summary>
    /// Used by DI container to create type.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="model">Model providing encapsulated functionality</param>
    /// <param name="uiScheduler">UI scheduler for thread marshalling</param>
    /// <exception cref="ArgumentNullException"></exception>
    [UsedImplicitly]
    public MainWindowClientViewModel(
        ILogger logger,
        IMainWindowModel model,
        IScheduler uiScheduler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _uiScheduler = uiScheduler ?? throw new ArgumentNullException(nameof(uiScheduler));

        ExitCommand = new DelegateCommand(OnExit);
        ConnectCommand = new DelegateCommand(async () => await OnConnectAsync());
        DisconnectCommand = new DelegateCommand(async () => await OnDisconnectAsync());
        LoadedCommand = new AsyncCommand(OnLoadedAsync);

        // Initialize the background image (solid RGB(37,37,37))
        BackgroundImage = CreateBackgroundImage();

        WireUpObservables();
    }


    /// <summary>
    /// Required for WPF design-time support.
    /// </summary>
    [UsedImplicitly]
    public MainWindowClientViewModel()
    {
        _logger = LogManager.GetCurrentClassLogger();
        _model = new MainWindowModel(
            new DesignTimeClientContext(),
            new DesignTimeApplicationLifetime(),
            new DesignTimePipeClient());
        _uiScheduler = CurrentThreadScheduler.Instance;

        ExitCommand = new DelegateCommand(() => { });
        ConnectCommand = new DelegateCommand(() => { });
        DisconnectCommand = new DelegateCommand(() => { });
        LoadedCommand = new AsyncCommand(() => Task.CompletedTask);

        // Initialize the background image for design-time
        BackgroundImage = CreateBackgroundImage();

        ClientText = "READY: Client (Design)";
        BorderIsVisible = true;
    }

    public DelegateCommand ExitCommand { get; }
    public DelegateCommand ConnectCommand { get; }
    public DelegateCommand DisconnectCommand { get; }
    public AsyncCommand LoadedCommand { get; } // Changed to AsyncCommand for async loading

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value, nameof(Title));
    }

    // Desired content/client size exposed to the ViewModel (client area width/height).
    // These represent the size of the content area (the Grid inside the window).
    private double _windowContentWidth = 288;
    private double _windowContentHeight = 288;

    // Backing field for BorderIsVisible. Default is true.
    private bool _borderIsVisible = true;

    // Backing field for ClientText. Default visible text shown in the UI.
    private string _clientText = "READY: Client";
    private TimeSpan _captionHideDelay;

    public double WindowContentWidth
    {
        get => _windowContentWidth;
        set => SetProperty(ref _windowContentWidth, value, nameof(WindowContentWidth));
    }

    public double WindowContentHeight
    {
        get => _windowContentHeight;
        set => SetProperty(ref _windowContentHeight, value, nameof(WindowContentHeight));
    }

    /// <summary>
    /// Controls whether the window border/caption are visible.
    /// True by default; set to false when a Connected state is received.
    /// </summary>
    public bool BorderIsVisible
    {
        get => _borderIsVisible;
        set => SetProperty(ref _borderIsVisible, value, nameof(BorderIsVisible));
    }

    /// <summary>
    /// Text shown in the main ClientText TextBlock.
    /// Default is "READY: Client".
    /// </summary>
    public string ClientText
    {
        get => _clientText;
        set => SetProperty(ref _clientText, value, nameof(ClientText));
    }

    public void Dispose() => _disposables.Dispose();

    private void WireUpObservables()
    {
        // Ensure we observe on the UI scheduler so setters run on GUI thread
        _disposables.Add(_model.ConnectionChanges
            .ObserveOn(_uiScheduler)
            .Subscribe(state => Title = $"Client {_model.GetClientId()}: {state.State}"));

        _disposables.Add(_model.Coordinates
            .ObserveOn(_uiScheduler)
            .Subscribe(c => Title = $"Client {_model.GetClientId()}: ({c.X:0.###}, {c.Y:0.###})"));

        // While coordinates are being received, hide the border/caption (BorderIsVisible = false).
        // If no coordinate arrives for 5 seconds, emit true to show the border again.
        _disposables.Add(
            _model.Coordinates
                // For each coordinate start a sequence that immediately yields 'false'
                // and then yields 'true' after 5 seconds; Switch ensures the timer restarts on new coords.
                .Select(_ =>
                {
                    _captionHideDelay = TimeSpan.FromSeconds(5);
                    return Observable.Timer(_captionHideDelay, _uiScheduler)
                        .Select(__ => true)
                        .StartWith(false);
                })
                .Switch()
                // Ensure the stream emits an initial 'true' so the border is visible at startup
                .StartWith(true)
                .ObserveOn(_uiScheduler)
                .Subscribe(visible => BorderIsVisible = visible)
        );

        // Toggle border visibility based on connection state:
        // When Connected -> hide border/caption (BorderIsVisible = false).
        // When Disconnected -> show border/caption (BorderIsVisible = true).
        _disposables.Add(_model.ConnectionChanges
            .ObserveOn(_uiScheduler)
            .Subscribe(state =>
            {
                switch (state.State)
                {
                    case ConnectionState.Connected:
                        BorderIsVisible = false;
                        break;
                    case ConnectionState.Disconnected:
                        BorderIsVisible = true;
                        break;
                    default:
                        // For any unknown/future states, show the border by default.
                        BorderIsVisible = true;
                        break;
                }
            }));
    }


    private async Task OnLoadedAsync()
    {
        try
        {
            _logger.Trace("MainWindow loaded.");

            await _model.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        }
        catch (Exception e)
        {
            _logger.Error(e);
        }
    }

    private async Task OnConnectAsync()
    {
        try
        {
            await _model.ConnectAsync();
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ConnectAsync failed");
        }
    }

    private async Task OnDisconnectAsync()
    {
        try
        {
            await _model.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "DisconnectAsync failed");
        }
    }

    private void OnExit()
    {
        _logger.Info("Shutdown requested by user from Client VM (ClientId={ClientId})", _model.GetClientId());
        _model.RequestShutdown();
    }
}