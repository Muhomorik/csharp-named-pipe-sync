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

        ExitCommand = new DelegateCommand(() =>
        {
            /* no-op at design time */
        });
        ConnectCommand = new DelegateCommand(() => { });
        DisconnectCommand = new DelegateCommand(() => { });
        LoadedCommand = new AsyncCommand(async () =>
        {
            /* no-op at design time */
        });
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
