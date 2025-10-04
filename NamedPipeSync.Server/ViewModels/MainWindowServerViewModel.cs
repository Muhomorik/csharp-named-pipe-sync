using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;

using DevExpress.Mvvm;

using JetBrains.Annotations;

using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Domain.Events;
using NamedPipeSync.Common.Infrastructure.Protocol;
using NamedPipeSync.Server.Services;

using NLog;

namespace NamedPipeSync.Server.ViewModels;

// TODO: update status bat if client exe is not found.

public class MainWindowServerViewModel : ViewModelBase
{
    private readonly IClientWithRuntimeRepository _repository;
        private readonly IClientWithRuntimeEventDispatcher _events;
    private readonly IClientProcessLauncher _launcher;
    private readonly ILogger _logger;
    private readonly INamedPipeServer _server;
    private readonly IScheduler _uiScheduler;

    private string _title = "Server";


    /// <summary>
    /// Used by DI container to create type.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="server"></param>
    /// <param name="repository"></param>
    /// <param name="events"></param>
    /// <param name="launcher"></param>
    /// <exception cref="ArgumentNullException"></exception>
    [UsedImplicitly]
    public MainWindowServerViewModel(
        ILogger logger,
        INamedPipeServer server,
        IClientWithRuntimeRepository repository,
        IClientWithRuntimeEventDispatcher events,
        IClientProcessLauncher launcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _uiScheduler = DispatcherScheduler.Current;

        Title = "NamedPipeSync Server";

        // React to connection changes to keep UI connection state in sync and ensure repository has the client entry
        _server.ConnectionChanged
            .ObserveOn(_uiScheduler)
            .Subscribe(change =>
            {
                if (!_repository.TryGet(change.ClientId, out var client))
                {
                    // Seed using checkpoint with same id when available, otherwise use first checkpoint
                    var cp = Checkpoints.Start.FirstOrDefault(c => c.Id == change.ClientId.Id);
                    if (cp.Id == 0 && Checkpoints.Start.Count > 0)
                    {
                        cp = Checkpoints.Start[0];
                    }
                    client = new ClientWithRuntime(change.ClientId, cp);
                }
                client!.Connection = change.State;
                _repository.Update(client);
                UpsertClientState(client);
            });

        // Subscribe to domain events by type
        _events.Events
            .ObserveOn(_uiScheduler)
            .OfType<CoordinatesUpdated>()
            .Subscribe(evt =>
            {
                if (_repository.TryGet(evt.ClientId, out var client))
                {
                    // Client runtime state is already updated by calculators/schedulers; reflect it in UI
                    UpsertClientState(client!);
                }
            });

        _events.Events
            .ObserveOn(_uiScheduler)
            .OfType<CheckpointReached>()
            .Subscribe(evt =>
            {
                // Currently, just refresh UI from repository snapshot
                if (_repository.TryGet(evt.ClientId, out var client))
                {
                    UpsertClientState(client!);
                }
            });

        StartClientCommand = new AsyncCommand<int>(StartClientAsync);
        StartAllCommand = new AsyncCommand(StartAllAsync);
        LoadedCommand = new DelegateCommand(OnWindowLoaded);
        CloseAllClientsCommand = new AsyncCommand(CloseAllClientsAsync);
        ToggleClientCommand = new AsyncCommand<int>(ToggleClientAsync);
    }

    /// <summary>
    /// Required for WPF design-time support.
    /// </summary>
    [UsedImplicitly]
    public MainWindowServerViewModel()
    {
        _logger = LogManager.GetCurrentClassLogger();
        _server = null!;
        _repository = null!;
        _events = null!;
        _launcher = null!;
        _uiScheduler = DispatcherScheduler.Current;

        // Initialize commands with harmless placeholders so bindings can safely call them.
        StartClientCommand = new AsyncCommand<int>(_ => Task.CompletedTask);
        StartAllCommand = new AsyncCommand(() => Task.CompletedTask);
        LoadedCommand = new DelegateCommand(() => { });
        CloseAllClientsCommand = new AsyncCommand(() => Task.CompletedTask);
        ToggleClientCommand = new AsyncCommand<int>(_ => Task.CompletedTask);
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

    public bool CanStartAll => Clients.Any(c => c.Connection != ConnectionState.Connected);

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value, nameof(Title));
    }

    private void OnWindowLoaded()
    {
        // Seed repository with clients based on available start checkpoints (players == checkpoints)
        foreach (var cp in Checkpoints.Start)
        {
            var id = new ClientId(cp.Id);
            if (!_repository.TryGet(id, out var existing))
            {
                var client = new ClientWithRuntime(id, cp)
                {
                    Coordinates = cp.Location,
                    Connection = ConnectionState.Disconnected,
                    IsOnCheckpoint = true
                };
                _repository.Update(client);
            }
        }

        // Initialize UI from repository snapshot
        var all = _repository.GetAll();
        foreach (var c in all)
        {
            UpsertClientState(c);
        }

        RaisePropertyChanged(nameof(CanStartAll));

        _server.Start();
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
        _launcher.StartClient(new ClientId(id));
        return Task.CompletedTask;
    }

    private Task StartAllAsync()
    {
        var ids = Clients.Select(c => new ClientId(c.Id));
        return _launcher.StartManyAsync(ids);
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

    private async Task CloseAllClientsAsync()
    {
        try
        {
            var ids = _server.ConnectedClientIds;
            var tasks = ids.Select(id => _server.SendCloseRequestAsync(id));
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "CloseAllClientsAsync failed");
        }
    }
}
