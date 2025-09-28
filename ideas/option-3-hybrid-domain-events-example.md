**File:** `option-3-hybrid-domain-events-example.md`

```markdown
# Option 3: Hybrid Approach - Pure Domain Entities with Application Service Event Publishing

This example shows how to implement domain events using pure domain entities (no infrastructure dependencies) with event publishing handled by application services.

## 1. Pure Domain Entity (No Infrastructure Dependencies)

**File: `NamedPipeSyncCommon/Domain/ClientWithRuntimeData.cs`**
```
csharp
using System;
using System.Diagnostics;
using NamedPipeSyncCommon.Application;

namespace NamedPipeSyncCommon.Domain;

[DebuggerDisplay("Id = {Id}, Conn = {Connection}, Coord = {Coordinates}, LastSeen = {LastSeenUtc}")]
public sealed class ClientWithRuntimeData
{
    public ClientWithRuntimeData(ClientId id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Connection = ConnectionState.Disconnected;
        Coordinates = new Coordinate(0, 0);
        LastSeenUtc = DateTimeOffset.UtcNow;
    }

    public ClientId Id { get; }
    public ConnectionState Connection { get; private set; }
    public Coordinate Coordinates { get; private set; }

    // Runtime metadata
    public DateTimeOffset LastSeenUtc { get; private set; }
    public string? PipeName { get; private set; }
    public int? ObservedSendBacklog { get; private set; }

    /// <summary>
    /// Updates connection state. Returns true if state changed.
    /// </summary>
    public bool SetConnection(ConnectionState state)
    {
        if (Connection != state)
        {
            Connection = state;
            Touch();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Updates coordinates. Returns true if coordinates changed.
    /// </summary>
    public bool SetCoordinates(Coordinate coords)
    {
        if (!coords.Equals(Coordinates))
        {
            Coordinates = coords;
            Touch();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Updates runtime metadata. Returns true if any value changed.
    /// </summary>
    public bool UpdateRuntime(DateTimeOffset? lastSeenUtc = null, string? pipeName = null, int? observedSendBacklog = null)
    {
        var changed = false;
        
        if (lastSeenUtc.HasValue && LastSeenUtc != lastSeenUtc.Value)
        {
            LastSeenUtc = lastSeenUtc.Value;
            changed = true;
        }
        
        if (pipeName is not null && PipeName != pipeName)
        {
            PipeName = pipeName;
            changed = true;
        }
        
        if (observedSendBacklog.HasValue && ObservedSendBacklog != observedSendBacklog.Value)
        {
            ObservedSendBacklog = observedSendBacklog;
            changed = true;
        }

        return changed;
    }

    private void Touch() => LastSeenUtc = DateTimeOffset.UtcNow;
}
```
## 2. Event Records (Domain Layer)

**File: `NamedPipeSyncCommon/Domain/ClientRuntimeEvents.cs`**
```
csharp
using NamedPipeSyncCommon.Application;

namespace NamedPipeSyncCommon.Domain;

public readonly record struct CoordinatesChanged(ClientId Id, Coordinate Coordinates);
public readonly record struct ConnectionChanged(ClientId Id, ConnectionState State);
public readonly record struct RuntimeDataUpdated(ClientId Id, DateTimeOffset LastSeenUtc, string? PipeName, int? ObservedSendBacklog);
```
## 3. Application Service with Event Publishing

**File: `NamedPipeSyncCommon/Application/ClientRuntimeService.cs`**

```csharp
using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeSyncCommon.Domain;
using NLog;

namespace NamedPipeSyncCommon.Application;

public interface IClientRuntimeService : IDisposable
{
    IObservable<CoordinatesChanged> CoordinatesChanged { get; }
    IObservable<ConnectionChanged> ConnectionChanged { get; }
    IObservable<RuntimeDataUpdated> RuntimeUpdated { get; }
    
    Task<ClientWithRuntimeData?> GetClientAsync(ClientId id, CancellationToken ct = default);
    Task<ClientWithRuntimeData> GetOrCreateClientAsync(ClientId id, CancellationToken ct = default);
    Task UpdateCoordinatesAsync(ClientId id, Coordinate coordinates, CancellationToken ct = default);
    Task UpdateConnectionAsync(ClientId id, ConnectionState state, CancellationToken ct = default);
    Task UpdateRuntimeDataAsync(ClientId id, DateTimeOffset? lastSeenUtc = null, string? pipeName = null, int? observedSendBacklog = null, CancellationToken ct = default);
    Task<bool> RemoveClientAsync(ClientId id, CancellationToken ct = default);
}

public sealed class ClientRuntimeService : IClientRuntimeService
{
    private readonly ILogger _logger;
    private readonly IClientWithRuntimeDataRepository _repository;
    
    private readonly Subject<CoordinatesChanged> _coordinatesChanged = new();
    private readonly Subject<ConnectionChanged> _connectionChanged = new();
    private readonly Subject<RuntimeDataUpdated> _runtimeUpdated = new();

    public ClientRuntimeService(ILogger logger, IClientWithRuntimeDataRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public IObservable<CoordinatesChanged> CoordinatesChanged => _coordinatesChanged;
    public IObservable<ConnectionChanged> ConnectionChanged => _connectionChanged;
    public IObservable<RuntimeDataUpdated> RuntimeUpdated => _runtimeUpdated;

    public async Task<ClientWithRuntimeData?> GetClientAsync(ClientId id, CancellationToken ct = default)
    {
        return await _repository.TryGetAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<ClientWithRuntimeData> GetOrCreateClientAsync(ClientId id, CancellationToken ct = default)
    {
        var client = await _repository.TryGetAsync(id, ct).ConfigureAwait(false);
        if (client == null)
        {
            client = new ClientWithRuntimeData(id);
            await _repository.UpsertAsync(client, ct).ConfigureAwait(false);
            _logger.Info($"Created new runtime client: {id}");
        }
        return client;
    }

    public async Task UpdateCoordinatesAsync(ClientId id, Coordinate coordinates, CancellationToken ct = default)
    {
        try
        {
            var client = await GetOrCreateClientAsync(id, ct).ConfigureAwait(false);
            
            if (client.SetCoordinates(coordinates))
            {
                await _repository.UpsertAsync(client, ct).ConfigureAwait(false);
                _coordinatesChanged.OnNext(new CoordinatesChanged(id, coordinates));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            throw;
        }
    }

    public async Task UpdateConnectionAsync(ClientId id, ConnectionState state, CancellationToken ct = default)
    {
        try
        {
            var client = await GetOrCreateClientAsync(id, ct).ConfigureAwait(false);
            
            if (client.SetConnection(state))
            {
                await _repository.UpsertAsync(client, ct).ConfigureAwait(false);
                _connectionChanged.OnNext(new ConnectionChanged(id, state));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            throw;
        }
    }

    public async Task UpdateRuntimeDataAsync(ClientId id, DateTimeOffset? lastSeenUtc = null, string? pipeName = null, int? observedSendBacklog = null, CancellationToken ct = default)
    {
        try
        {
            var client = await GetOrCreateClientAsync(id, ct).ConfigureAwait(false);
            
            if (client.UpdateRuntime(lastSeenUtc, pipeName, observedSendBacklog))
            {
                await _repository.UpsertAsync(client, ct).ConfigureAwait(false);
                _runtimeUpdated.OnNext(new RuntimeDataUpdated(id, client.LastSeenUtc, client.PipeName, client.ObservedSendBacklog));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            throw;
        }
    }

    public async Task<bool> RemoveClientAsync(ClientId id, CancellationToken ct = default)
    {
        try
        {
            return await _repository.RemoveAsync(id, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            throw;
        }
    }

    public void Dispose()
    {
        _coordinatesChanged.Dispose();
        _connectionChanged.Dispose();
        _runtimeUpdated.Dispose();
    }
}
```
```


## 4. Simplified Event Dispatcher

**File: `NamedPipeSyncCommon/Application/ClientRuntimeEventDispatcher.cs`**

```csharp
using System;
using System.Reactive.Linq;
using NamedPipeSyncCommon.Domain;
using NLog;

namespace NamedPipeSyncCommon.Application;

/// <summary>
/// Aggregates events from ClientRuntimeService and repository changes into unified streams.
/// </summary>
public interface IClientRuntimeEventDispatcher : IDisposable
{
    IObservable<CoordinatesChanged> CoordinatesChanged { get; }
    IObservable<ConnectionChanged> ConnectionChanged { get; }
    IObservable<RuntimeDataUpdated> RuntimeUpdated { get; }
    IObservable<ClientWithRuntimeDataChange> RepositoryChanges { get; }
}

public sealed class ClientRuntimeEventDispatcher : IClientRuntimeEventDispatcher
{
    private readonly ILogger _logger;
    private readonly IDisposable _subscription;

    public ClientRuntimeEventDispatcher(
        ILogger logger, 
        IClientRuntimeService clientService,
        IClientWithRuntimeDataRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Forward events from the service (which handles business logic)
        CoordinatesChanged = clientService.CoordinatesChanged;
        ConnectionChanged = clientService.ConnectionChanged;
        RuntimeUpdated = clientService.RuntimeUpdated;
        
        // Repository changes (add/remove clients)
        RepositoryChanges = repository.Changes;

        // Optional: Log errors
        _subscription = Observable.Merge(
                CoordinatesChanged.Select(_ => Unit.Default),
                ConnectionChanged.Select(_ => Unit.Default),
                RuntimeUpdated.Select(_ => Unit.Default),
                RepositoryChanges.Select(_ => Unit.Default)
            )
            .Subscribe(_ => { }, ex => _logger.Error(ex));
    }

    public IObservable<CoordinatesChanged> CoordinatesChanged { get; }
    public IObservable<ConnectionChanged> ConnectionChanged { get; }
    public IObservable<RuntimeDataUpdated> RuntimeUpdated { get; }
    public IObservable<ClientWithRuntimeDataChange> RepositoryChanges { get; }

    public void Dispose() => _subscription.Dispose();
}

// Helper for the merge operation
internal readonly struct Unit
{
    public static readonly Unit Default = new();
}
```


## 5. Updated CoordinateBroadcaster

**File: `NamedPipeSync/Services/CoordinateBroadcaster.cs`**

```csharp
using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeSyncCommon.Application;
using NLog;

namespace NamedPipeSync.Services;

public sealed class CoordinateBroadcaster : ICoordinateBroadcaster
{
    private readonly ILogger _logger;
    private readonly INamedPipeServer _server;
    private readonly ICoordinatesCalculator _calculator;
    private readonly IClientRuntimeService _clientService; // Use service instead of repository
    private readonly IScheduler _scheduler;

    private IDisposable? _subscription;

    public CoordinateBroadcaster(
        ILogger logger,
        INamedPipeServer server,
        ICoordinatesCalculator calculator,
        IClientRuntimeService clientService, // Changed from repository
        IScheduler scheduler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    public IDisposable Start()
    {
        _subscription?.Dispose();
        _subscription = Observable
            .Interval(TimeSpan.FromSeconds(1), _scheduler)
            .SelectMany(_ => Observable.FromAsync(ct => BroadcastOnceAsync(ct)))
            .Subscribe(_ => { }, ex => _logger.Error(ex));
        return _subscription;
    }

    public void Dispose() => _subscription?.Dispose();

    private async Task BroadcastOnceAsync(CancellationToken ct)
    {
        var connectedIds = _server.ConnectedClientIds;
        if (connectedIds.Count == 0) return;

        foreach (var id in connectedIds)
        {
            try
            {
                var client = await _clientService.GetClientAsync(id, ct).ConfigureAwait(false);
                if (client == null) continue;

                var current = client.Coordinates;
                var next = _calculator.NextCoordinates(client.Id, current);

                // Send to client
                await _server.SendCoordinateAsync(client.Id, next.X, next.Y).ConfigureAwait(false);
                
                // Update coordinates via service (triggers events)
                await _clientService.UpdateCoordinatesAsync(client.Id, next, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }
    }
}
```


## Key Benefits of This Approach

1. **Pure Domain Entities**: No infrastructure dependencies, easy to test
2. **Centralized Event Publishing**: All events flow through the service layer
3. **Clear Separation**: Domain handles business rules, Application handles orchestration and events
4. **Consistent API**: All client updates go through the service with proper event emission
5. **Better Testability**: Can mock `IClientRuntimeService` easily
6. **Memory Management**: Service handles Subject disposal, not individual entities

## Usage Example

```csharp
// Instead of directly manipulating entities:
// client.SetCoordinates(newCoords); // No events emitted

// Use the service:
await clientService.UpdateCoordinatesAsync(clientId, newCoords); // Events emitted automatically
```


## DI Registration Example

```csharp
builder.RegisterType<InMemoryClientWithRuntimeDataRepository>()
    .As<IClientWithRuntimeDataRepository>()
    .SingleInstance();

builder.RegisterType<ClientRuntimeService>()
    .As<IClientRuntimeService>()
    .SingleInstance();

builder.RegisterType<ClientRuntimeEventDispatcher>()
    .As<IClientRuntimeEventDispatcher>()
    .SingleInstance();

builder.RegisterType<CoordinateBroadcaster>()
    .As<ICoordinateBroadcaster>()
    .SingleInstance();
```


This approach maintains your reactive patterns while keeping the domain layer clean and focused on business logic, with all event publishing centralized in the application service layer.
```
The markdown file has been created in the same directory as README.md. This approach addresses your concern about having Subjects inside domain entities by moving all the reactive infrastructure to the application service layer, keeping your domain entities pure and focused on business logic.
```
