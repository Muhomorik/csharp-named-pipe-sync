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
    private readonly IApplicationLifetime _appLifetime;
    private readonly IClientContext _clientContext;

    private readonly CompositeDisposable _disposables = new();
    private readonly IScheduler _uiScheduler;

    private readonly INamedPipeClient _pipeClient;

    private string _title = "VM: Client Window";


    /// <summary>
    /// Used by DI container to create type.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="clientContext"></param>
    /// <param name="appLifetime"></param>
    /// <param name="pipeClient"></param>
    /// <param name="uiScheduler"></param>
    /// <exception cref="ArgumentNullException"></exception>
    [UsedImplicitly]
    public MainWindowClientViewModel(
        ILogger logger,
        IClientContext clientContext,
        IApplicationLifetime appLifetime,
        INamedPipeClient pipeClient,
        IScheduler uiScheduler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
        _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
        _pipeClient = pipeClient ?? throw new ArgumentNullException(nameof(pipeClient));
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
        _clientContext = new DesignTimeClientContext();
        _appLifetime = new DesignTimeApplicationLifetime();
        _pipeClient = new DesignTimePipeClient();
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

    public void Dispose() => _disposables.Dispose();

    private void WireUpObservables()
    {
        // Ensure we observe on the UI scheduler so setters run on GUI thread
        _disposables.Add(_pipeClient.ConnectionChanged
            .ObserveOn(_uiScheduler)
            .Subscribe(state => Title = $"Client {_clientContext.ClientId}: {state.State}"));

        _disposables.Add(_pipeClient.Coordinates
            .ObserveOn(_uiScheduler)
            .Subscribe(c => Title = $"Client {_clientContext.ClientId}: ({c.X:0.###}, {c.Y:0.###})"));
    }


    private async Task OnLoadedAsync()
    {
        try
        {
            _logger.Trace("MainWindow loaded.");

            await _pipeClient.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
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
            await _pipeClient.ConnectAsync();
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
            await _pipeClient.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "DisconnectAsync failed");
        }
    }

    private void OnExit()
    {
        _logger.Info("Shutdown requested by user from Client VM (ClientId={ClientId})", _clientContext.ClientId);
        _appLifetime.Shutdown();
    }
}
