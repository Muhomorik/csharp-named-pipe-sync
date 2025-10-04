using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;

using DevExpress.Mvvm;

using JetBrains.Annotations;

using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Domain.Events;
using NamedPipeSync.Common.Infrastructure.Protocol;
using NamedPipeSync.Server.Models;

using NLog;

namespace NamedPipeSync.Server.ViewModels;

// TODO: update status bat if client exe is not found.

public class MainWindowServerViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger _logger;
    private readonly IScheduler _uiScheduler;
    private readonly IMainWindowServerModel _model;
    private readonly CompositeDisposable _disposables = new();

    private string _title = "Server";
    private readonly bool _isClientExecutableMissing;

    /// <summary>
    /// Used by DI container to create type.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="uiScheduler">Scheduler used to marshal events to the UI thread (injected for testability)</param>
    /// <param name="model"></param>
    /// <param name="isClientExecutableMissing">True when the configured client executable path does not exist on disk.</param>
    /// <exception cref="ArgumentNullException"></exception>
    [UsedImplicitly]
    public MainWindowServerViewModel(
        ILogger logger,
        IScheduler uiScheduler,
        IMainWindowServerModel model,
        bool isClientExecutableMissing)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _uiScheduler = uiScheduler ?? throw new ArgumentNullException(nameof(uiScheduler));
        _isClientExecutableMissing = isClientExecutableMissing;

        Title = "NamedPipeSync Server";

        // React to connection changes to keep UI connection state in sync and ensure repository has the client entry
        _disposables.Add(
            _model.ConnectionChanged
                .ObserveOn(_uiScheduler)
                .Subscribe(change =>
                {
                    _model.EnsureClientEntryOnConnectionChange(change.ClientId, change.State);
                    if (_model.TryGet(change.ClientId, out var client))
                    {
                        UpsertClientState(client!);
                    }
                }));

        // Subscribe to domain events by type
        _disposables.Add(
            _model.Events
                .OfType<CoordinatesUpdated>()
                // Reduce UI churn from high-frequency updates; keep UI smooth
                .Sample(TimeSpan.FromMilliseconds(100))
                .ObserveOn(_uiScheduler)
                .Subscribe(evt =>
                {
                    if (_model.TryGet(evt.ClientId, out var client))
                    {
                        // Client runtime state is already updated by calculators/schedulers; reflect it in UI
                        UpsertClientState(client!);
                    }
                }));

        _disposables.Add(
            _model.Events
                .ObserveOn(_uiScheduler)
                .OfType<CheckpointReached>()
                .Subscribe(evt =>
                {
                    // Currently, just refresh UI from repository snapshot
                    if (_model.TryGet(evt.ClientId, out var client))
                    {
                        UpsertClientState(client!);
                    }
                }));

        StartClientCommand = new AsyncCommand<int>(StartClientAsync);
        StartAllCommand = new AsyncCommand(StartAllAsync);
        LoadedCommand = new DelegateCommand(OnWindowLoaded);
        CloseAllClientsCommand = new AsyncCommand(CloseAllClientsAsync);
        ToggleClientCommand = new AsyncCommand<int>(ToggleClientAsync);
        StartSendingCommand = new AsyncCommand(StartSendingAsync);
        StopSendingCommand = new AsyncCommand(StopSendingAsync);
        ResetPositionCommand = new AsyncCommand(ResetPositionAsync);
    }

    /// <summary>
    /// Required for WPF design-time support.
    /// </summary>
    [UsedImplicitly]
    public MainWindowServerViewModel()
    {
        _logger = LogManager.GetCurrentClassLogger();
        _uiScheduler = DispatcherScheduler.Current;
        _model = null!;
        _isClientExecutableMissing = false;

        // Initialize commands with harmless placeholders so bindings can safely call them.
        StartClientCommand = new AsyncCommand<int>(_ => Task.CompletedTask);
        StartAllCommand = new AsyncCommand(() => Task.CompletedTask);
        LoadedCommand = new DelegateCommand(() => { });
        CloseAllClientsCommand = new AsyncCommand(() => Task.CompletedTask);
        ToggleClientCommand = new AsyncCommand<int>(_ => Task.CompletedTask);
        StartSendingCommand = new AsyncCommand(() => Task.CompletedTask);
        StopSendingCommand = new AsyncCommand(() => Task.CompletedTask);
        ResetPositionCommand = new AsyncCommand(() => Task.CompletedTask);
    }

    protected override void OnInitializeInDesignMode()
    {
        base.OnInitializeInDesignMode();

        // Populate Clients with six fake coordinates (x, y top-left):
        // Row 0: (48,208), (368,208), (688,208)
        // Row 1: (48,528), (368,528), (688,528)
        Clients.Add(new ClientWithStateBindingItem { Id = 1, Connection = ConnectionState.Connected, X = 48, Y = 208 });
        Clients.Add(new ClientWithStateBindingItem
            { Id = 2, Connection = ConnectionState.Disconnected, X = 368, Y = 208 });
        Clients.Add(new ClientWithStateBindingItem
            { Id = 3, Connection = ConnectionState.Connected, X = 688, Y = 208 });
        Clients.Add(new ClientWithStateBindingItem { Id = 4, Connection = ConnectionState.Connected, X = 48, Y = 528 });
        Clients.Add(new ClientWithStateBindingItem
            { Id = 5, Connection = ConnectionState.Connected, X = 368, Y = 528 });
        Clients.Add(new ClientWithStateBindingItem
            { Id = 6, Connection = ConnectionState.Connected, X = 688, Y = 528 });
    }

    public ObservableCollection<ClientWithStateBindingItem> Clients { get; } = new();

    public ICommand StartClientCommand { get; }
    public ICommand StartAllCommand { get; }
    public ICommand LoadedCommand { get; }

    public ICommand CloseAllClientsCommand { get; }

    // New command for per-row actions
    public ICommand ToggleClientCommand { get; }

    public ICommand StartSendingCommand { get; }
    public ICommand StopSendingCommand { get; }
    public ICommand ResetPositionCommand { get; }

    /// <summary>
    /// Message shown in the status bar. When the client executable is missing this will contain a warning.
    /// </summary>
    public string StatusBarMessage => _isClientExecutableMissing
        ? "Warning: client executable not found"
        : "Tip: ¯\\_(ツ)_/¯";

    public bool CanStartAll => Clients.Any(c => c.Connection != ConnectionState.Connected);

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value, nameof(Title));
    }

    private void OnWindowLoaded()
    {
        // Seed repository with clients based on available start checkpoints (players == checkpoints)
        _model.SeedMissingClients();

        // Initialize UI from a repository snapshot
        var all = _model.GetAllClients();
        foreach (var c in all)
        {
            UpsertClientState(c);
        }

        RaisePropertyChanged(nameof(CanStartAll));

        _model.StartServer();
    }

    private void UpsertClientState(ClientWithRuntime client)
    {
        var existing = Clients.FirstOrDefault(x => x.Id == client.Id.Id);
        if (existing == null)
        {
            var bItem = new ClientWithStateBindingItem
            {
                Id = client.Id.Id,
                Connection = client.Connection,
                X = client.Coordinates.X,
                Y = client.Coordinates.Y
            };
            Clients.Add(bItem);
            RaisePropertyChanged(nameof(CanStartAll));
        }
        else
        {
            existing.Connection = client.Connection;
            existing.X = client.Coordinates.X;
            existing.Y = client.Coordinates.Y;
            RaisePropertyChanged(nameof(Clients));
            RaisePropertyChanged(nameof(CanStartAll));
        }
    }

    private Task StartClientAsync(int id)
    {
        _model.StartClient(new ClientId(id));
        return Task.CompletedTask;
    }

    private Task StartAllAsync()
    {
        var ids = Clients.Select(c => new ClientId(c.Id));
        return _model.StartManyAsync(ids);
    }

    private Task ToggleClientAsync(int id)
    {
        var client = Clients.FirstOrDefault(c => c.Id == id);
        if (client == null)
        {
            return Task.CompletedTask;
        }

        if (client.Connection != ConnectionState.Connected)
        {
            return StartClientAsync(id);
        }

        return Task.CompletedTask;
    }

    private Task StartSendingAsync()
    {
        _model.StartSending();
        return Task.CompletedTask;
    }

    private Task StopSendingAsync()
    {
        _model.StopSending();
        return Task.CompletedTask;
    }

    private async Task ResetPositionAsync()
    {
        try
        {
            await _model.ResetPositionAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
        }
    }

    private async Task CloseAllClientsAsync()
    {
        try
        {
            await _model.CloseAllClientsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
