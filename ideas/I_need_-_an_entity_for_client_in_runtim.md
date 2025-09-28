### Overview
You’re asking for four related building blocks across the shared library and server app layers:

- A domain entity representing a connected client with runtime metadata: `ClientWithRuntimeData`.
- A repository to store and query that runtime entity.
- A dispatcher for its domain events (clean separation from UI/process lifetime).
- A service that advances existing coordinates and sends them via the server to clients (completes the TODO in `CoordinateBroadcaster`).

Below is a minimal, DDD-aligned design that fits your current structure and conventions (Reactive streams for intents, async I/O, NLog for logging, no process termination outside Presentation).

---

### 1) Domain entity: `ClientWithRuntimeData`
This entity lives in `NamedPipeSyncCommon.Domain` (shared, UI‑free). It complements your existing `Client` by holding runtime-only data and emitting domain events as `IObservable<T>` (in line with your current `Client.CoordinatesChanged`).

```csharp
using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using NamedPipeSyncCommon.Application; // for Coordinate, ConnectionState, ClientId

namespace NamedPipeSyncCommon.Domain;

[DebuggerDisplay("Id = {Id}, Conn = {Connection}, Coord = {Coordinates}, LastSeen = {LastSeenUtc}")]
public sealed class ClientWithRuntimeData
{
    private readonly Subject<CoordinatesChanged> _coordinatesChanged = new();
    private readonly Subject<ConnectionChanged> _connectionChanged = new();
    private readonly Subject<RuntimeDataUpdated> _runtimeUpdated = new();

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

    // Optional runtime metadata (non-persistent, evolves frequently)
    public DateTimeOffset LastSeenUtc { get; private set; }
    public string? PipeName { get; private set; }
    public int? ObservedSendBacklog { get; private set; }

    public IObservable<CoordinatesChanged> CoordinatesChanged => _coordinatesChanged;
    public IObservable<ConnectionChanged> ConnectionChanged => _connectionChanged;
    public IObservable<RuntimeDataUpdated> RuntimeUpdated => _runtimeUpdated;

    public void SetConnection(ConnectionState state)
    {
        if (Connection != state)
        {
            Connection = state;
            _connectionChanged.OnNext(new ConnectionChanged(Id, state));
            Touch();
        }
    }

    public void SetCoordinates(Coordinate coords)
    {
        if (!coords.Equals(Coordinates))
        {
            Coordinates = coords;
            _coordinatesChanged.OnNext(new CoordinatesChanged(Id, coords));
            Touch();
        }
    }

    public void UpdateRuntime(DateTimeOffset? lastSeenUtc = null, string? pipeName = null, int? observedSendBacklog = null)
    {
        if (lastSeenUtc.HasValue) LastSeenUtc = lastSeenUtc.Value;
        if (pipeName is not null) PipeName = pipeName;
        if (observedSendBacklog.HasValue) ObservedSendBacklog = observedSendBacklog;
        _runtimeUpdated.OnNext(new RuntimeDataUpdated(Id, LastSeenUtc, PipeName, ObservedSendBacklog));
    }

    private void Touch() => LastSeenUtc = DateTimeOffset.UtcNow;
}

public readonly record struct ConnectionChanged(ClientId Id, ConnectionState State);
public readonly record struct RuntimeDataUpdated(ClientId Id, DateTimeOffset LastSeenUtc, string? PipeName, int? ObservedSendBacklog);
```

Notes
- Keeps domain events UI-agnostic via `IObservable<T>`.
- Only signals intents; no shutdown logic, consistent with your guide.
- Uses your existing types (`ClientId`, `Coordinate`, `ConnectionState`).

---

### 2) Repository interface for runtime clients
The repository is an application-layer contract (belongs in `NamedPipeSyncCommon.Application`). Infrastructure can provide an in-memory implementation inside `NamedPipeSyncCommon.Infrastructure` (or the server app) without changing the contract.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeSyncCommon.Domain;

namespace NamedPipeSyncCommon.Application;

/// <summary>
/// Repository for ephemeral runtime client state. Implementations are typically in-memory
/// and scoped to the hosting process lifetime. Not for durable persistence.
/// </summary>
public interface IClientWithRuntimeDataRepository
{
    /// <summary>Insert or replace the entity for the given client.</summary>
    Task UpsertAsync(ClientWithRuntimeData client, CancellationToken ct);

    /// <summary>Try to get a client by id; returns null if missing.</summary>
    Task<ClientWithRuntimeData?> TryGetAsync(ClientId id, CancellationToken ct);

    /// <summary>Get a materialized snapshot of all clients (point-in-time copy).</summary>
    Task<IReadOnlyList<ClientWithRuntimeData>> SnapshotAsync(CancellationToken ct);

    /// <summary>
    /// Observable stream of repository-level change notifications. Emits when a client is
    /// added, updated, or removed. Implementations must be thread-safe and cold-start capable.
    /// </summary>
    IObservable<ClientWithRuntimeDataChange> Changes { get; }

    /// <summary>Remove a client.</summary>
    Task<bool> RemoveAsync(ClientId id, CancellationToken ct);
}

public readonly record struct ClientWithRuntimeDataChange(ChangeKind Kind, ClientWithRuntimeData Client);
public enum ChangeKind { Added, Updated, Removed }
```

Minimal in-memory implementation (Infrastructure) to get you going:

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeSyncCommon.Application;
using NamedPipeSyncCommon.Domain;
using NLog;

namespace NamedPipeSyncCommon.Infrastructure;

public sealed class InMemoryClientWithRuntimeDataRepository : IClientWithRuntimeDataRepository
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, ClientWithRuntimeData> _map = new();
    private readonly Subject<ClientWithRuntimeDataChange> _changes = new();

    public InMemoryClientWithRuntimeDataRepository(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IObservable<ClientWithRuntimeDataChange> Changes => _changes;

    public Task UpsertAsync(ClientWithRuntimeData client, CancellationToken ct)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        ct.ThrowIfCancellationRequested();

        _map.AddOrUpdate(client.Id.Value, client, static (_, __) => client);
        _changes.OnNext(new ClientWithRuntimeDataChange(
            _map.Count == 1 ? ChangeKind.Added : ChangeKind.Updated, client));
        return Task.CompletedTask;
    }

    public Task<ClientWithRuntimeData?> TryGetAsync(ClientId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _map.TryGetValue(id.Value, out var client);
        return Task.FromResult(client);
    }

    public Task<IReadOnlyList<ClientWithRuntimeData>> SnapshotAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var snapshot = _map.Values.ToArray();
        return Task.FromResult<IReadOnlyList<ClientWithRuntimeData>>(snapshot);
    }

    public Task<bool> RemoveAsync(ClientId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var removed = _map.TryRemove(id.Value, out var client);
        if (removed && client is not null)
        {
            _changes.OnNext(new ClientWithRuntimeDataChange(ChangeKind.Removed, client));
        }
        return Task.FromResult(removed);
    }
}
```

Notes
- Uses `NLog.ILogger` via DI per your conventions.
- Thread-safe; events via `Subject<T>` similar to your existing style.

---

### 3) Dispatcher for `ClientWithRuntimeData` domain events
This “dispatcher” turns domain-level entity events into application-level intents, without making any UI/process decisions. Presentation can subscribe and update the UI or trigger shutdown logic via its lifetime service if needed.

```csharp
using System;
using System.Reactive.Linq;
using NamedPipeSyncCommon.Application;
using NamedPipeSyncCommon.Domain;
using NLog;

namespace NamedPipeSyncCommon.Application;

/// <summary>
/// Aggregates domain events from <see cref="ClientWithRuntimeData"/> instances and the repository,
/// and exposes them as application-level observables. Does not perform UI or process actions.
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
    private readonly IClientWithRuntimeDataRepository _repo;

    private readonly IObservable<CoordinatesChanged> _coords;
    private readonly IObservable<ConnectionChanged> _conn;
    private readonly IObservable<RuntimeDataUpdated> _runtime;

    private readonly IObservable<ClientWithRuntimeDataChange> _repoChanges;
    private readonly IDisposable _subscription; // Keep refs to sources alive if needed

    public ClientRuntimeEventDispatcher(ILogger logger, IClientWithRuntimeDataRepository repo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));

        // Bridge repository item-level events into unified streams
        _repoChanges = repo.Changes.Publish().RefCount();

        _coords = _repoChanges
            .Where(c => c.Kind is ChangeKind.Added or ChangeKind.Updated)
            .SelectMany(c => c.Client.CoordinatesChanged);

        _conn = _repoChanges
            .Where(c => c.Kind is ChangeKind.Added or ChangeKind.Updated)
            .SelectMany(c => c.Client.ConnectionChanged);

        _runtime = _repoChanges
            .Where(c => c.Kind is ChangeKind.Added or ChangeKind.Updated)
            .SelectMany(c => c.Client.RuntimeUpdated);

        // Optional: keep a lightweight subscription to surface errors
        _subscription = _repoChanges.Subscribe(_ => { }, ex => _logger.Error(ex));
    }

    public IObservable<CoordinatesChanged> CoordinatesChanged => _coords;
    public IObservable<ConnectionChanged> ConnectionChanged => _conn;
    public IObservable<RuntimeDataUpdated> RuntimeUpdated => _runtime;
    public IObservable<ClientWithRuntimeDataChange> RepositoryChanges => _repoChanges;

    public void Dispose() => _subscription.Dispose();
}
```

Notes
- No Presentation dependencies; purely signals observables.
- Uses `.Publish().RefCount()` to avoid re-subscribing the repository stream unnecessarily.

---

### 4) Advance coordinates and send via server (complete `CoordinateBroadcaster`)
Your `NamedPipeSync.Services.CoordinateBroadcaster` already has the shape. Wire it to the runtime repository so it can look up current coordinates, compute new ones via `ICoordinatesCalculator`, and send via `INamedPipeServer`.

Key points
- Do not block the UI thread; keep the timer on a scheduler and use async.
- Take a snapshot of connected client IDs from the server, then join with repository data to compute next coordinates.
- Guard for disconnects between snapshot and send.

Concrete implementation:

```csharp
using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeSyncCommon.Application; // ICoordinatesCalculator, INamedPipeServer, ClientId, Coordinate
using NamedPipeSyncCommon.Domain;      // ClientWithRuntimeData
using NLog;

namespace NamedPipeSync.Services;

public sealed class CoordinateBroadcaster : ICoordinateBroadcaster
{
    private readonly ILogger _logger;
    private readonly INamedPipeServer _server;
    private readonly ICoordinatesCalculator _calculator;
    private readonly IClientWithRuntimeDataRepository _repo;
    private readonly IScheduler _scheduler;

    private IDisposable? _subscription;

    public CoordinateBroadcaster(
        ILogger logger,
        INamedPipeServer server,
        ICoordinatesCalculator calculator,
        IClientWithRuntimeDataRepository repo,
        IScheduler scheduler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
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
        // Snapshot of connected IDs at this instant.
        var ids = _server.ConnectedClientIds;
        if (ids.Count == 0) return;

        // Snapshot of runtime state.
        var clients = await _repo.SnapshotAsync(ct).ConfigureAwait(false);

        // Optional: index by id for quick lookup
        var byId = clients.ToDictionary(c => c.Id.Value, c => c);

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var client))
                continue; // client might have just disconnected or not yet tracked

            var current = client.Coordinates;
            var next = _calculator.NextCoordinates(client.Id, current);

            // Best-effort send; guard transient disconnect
            try
            {
                await _server.SendCoordinateAsync(client.Id, next.X, next.Y).ConfigureAwait(false);
                client.SetCoordinates(next); // Update runtime entity, triggers domain event
                await _repo.UpsertAsync(client, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }
    }
}
```

If your `ICoordinatesCalculator` currently expects all clients’ coordinates at once (as suggested by your TODO), offer an overload in the application layer:

```csharp
namespace NamedPipeSyncCommon.Application;

public interface ICoordinatesCalculator
{
    Coordinate NextCoordinates(ClientId id, Coordinate current);
    // Optional aggregate form if you prefer ring/relative movement
    Coordinate NextCoordinates(ClientId id, IReadOnlyDictionary<string, Coordinate> all);
}
```

Then use whichever suits your existing implementation (`SimpleRingCoordinatesCalculator`).

---

### Where each piece lives
- NamedPipeSyncCommon.Domain
  - `ClientWithRuntimeData` (+ its event records)
- NamedPipeSyncCommon.Application
  - `IClientWithRuntimeDataRepository`, `ClientWithRuntimeDataChange`
  - `IClientRuntimeEventDispatcher` (+ `ClientRuntimeEventDispatcher` implementation can also live here if it doesn’t need infra; otherwise put impl into Infrastructure)
- NamedPipeSyncCommon.Infrastructure
  - `InMemoryClientWithRuntimeDataRepository` (starter implementation)
- NamedPipeSync (WPF Server)
  - `CoordinateBroadcaster` uses server + repo + calculator. Keep the timer/scheduler non-UI blocking.

This keeps domain rules UI-free, and Infrastructure/presentation concerns out of Domain/Application.

---

### DI wiring hints (Rider/Autofac-style, based on your `App.xaml.cs` pattern)
- Register `InMemoryClientWithRuntimeDataRepository` as a singleton for the server process.
- Register `ClientRuntimeEventDispatcher` as a singleton that subscribes on app startup.
- Register `CoordinateBroadcaster` as a singleton; call `Start()` on app startup and dispose on shutdown.
- Ensure you resolve `ILogger` (NLog) as the first constructor parameter for new services.

Example registrations:

```csharp
builder.RegisterType<InMemoryClientWithRuntimeDataRepository>()
    .As<IClientWithRuntimeDataRepository>()
    .SingleInstance();

builder.RegisterType<ClientRuntimeEventDispatcher>()
    .As<IClientRuntimeEventDispatcher>()
    .SingleInstance();

builder.RegisterType<CoordinateBroadcaster>()
    .As<ICoordinateBroadcaster>()
    .SingleInstance();
```

Start on app launched:

```csharp
// var broadcaster = scope.Resolve<ICoordinateBroadcaster>();
// broadcaster.Start();
```

---

### Practical usage flow
1. Server accepts/loses connections; infrastructure updates `ClientWithRuntimeData` via the repository (connection state, pipe name, last-seen).
2. `ClientRuntimeEventDispatcher` surfaces events for interested application/presentation consumers.
3. `CoordinateBroadcaster` periodically computes the next coordinates for connected clients and sends them via `INamedPipeServer`, updating the runtime repository so downstream observers (e.g., UI) get fresh state.

---

### Next steps checklist
- Add the new entity and interfaces to `NamedPipeSyncCommon` as shown.
- Provide an in-memory repository implementation in Infrastructure.
- Wire them up in the server’s DI container and complete `CoordinateBroadcaster` using the repository.
- Build: `dotnet build NamedPipeSync.sln -c Debug`
- Run server then client to verify movement and event flow.

If you want, I can tailor the interfaces to exactly match the signatures of your existing `ICoordinatesCalculator` and `INamedPipeServer` (e.g., if `SendCoordinateAsync` takes primitive ids instead of `ClientId`).