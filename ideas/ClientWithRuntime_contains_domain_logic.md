### Summary
You’re right: `ClientWithRuntime` should not depend on `Subject` or `IDisposable`. The clean way to achieve this is to split the responsibilities:

- Keep the pure domain logic (proximity and transitions) in a dependency‑free component.
- Let `ClientWithRuntime` expose a simple, CLR event instead of an `IObservable`.
- If you still need reactive streams, put them in a separate adapter in the Application layer, not inside the domain logic wrapper.

This keeps the runtime logic testable and independent of Rx and disposal concerns, while still allowing consumers to opt into Rx via a thin adapter.

---

### Refactoring Plan
1) Move the proximity/transition algorithm into a pure, dependency‑free domain helper.

2) Make `ClientWithRuntime` a small orchestrator that:
- Holds the pure helper.
- Exposes `CurrentCheckpoint` (int?).
- Provides a method `OnCoordinate(Coordinate newCoords)` you can call whenever coordinates change.
- Raises a CLR event `CheckpointLeft` when a leave transition is detected.
- Has no references to `Subject`, `IObservable`, or `IDisposable`.

3) (Optional) If Rx is desired, create a separate `ClientRuntimeObservableAdapter` in the Application layer that turns the CLR event into an `IObservable<CheckpointLeft>` and subscribes to any Rx source (like `Client.CoordinatesChanged`) while owning the `IDisposable`. This keeps Rx out of your domain logic and out of `ClientWithRuntime`.

---

### Domain contracts (stay dependency‑free)
```csharp
namespace NamedPipeSync.Common.Domain;

using NamedPipeSync.Common.Infrastructure.Protocol;

public readonly record struct Checkpoint(int Number, Coordinate Location);

public readonly record struct CheckpointLeft(
    ClientId ClientId,
    int CheckpointNumber,
    Coordinate LastKnownCoordinates,
    DateTimeOffset At
);
```

---

### Pure domain helper (no Rx, no disposal)
```csharp
namespace NamedPipeSync.Common.Domain;

using System;
using System.Collections.Generic;
using System.Linq;
using NamedPipeSync.Common.Infrastructure.Protocol;

/// <summary>
/// Stateless-ish proximity tracker that computes checkpoint entry/leave transitions.
/// No Rx, no disposal, no UI/process concerns.
/// </summary>
public sealed class CheckpointProximityTracker
{
    private readonly IReadOnlyList<Checkpoint> _checkpoints;
    private readonly int _proximityPixelsSquared;

    public CheckpointProximityTracker(IEnumerable<Checkpoint> checkpoints, int proximityPixels = 3)
    {
        _checkpoints = (checkpoints ?? throw new ArgumentNullException(nameof(checkpoints))).ToList();
        _proximityPixelsSquared = proximityPixels * proximityPixels;
    }

    public int? Evaluate(
        ClientId clientId,
        Coordinate current,
        int? currentCheckpoint,
        bool isCurrentlyInside,
        out bool isInsideNow,
        out CheckpointLeft? leftEvent)
    {
        leftEvent = null;
        isInsideNow = isCurrentlyInside;

        // If we were inside, check whether we left that checkpoint’s radius.
        if (currentCheckpoint is int active && isCurrentlyInside)
        {
            var existing = _checkpoints.FirstOrDefault(c => c.Number == active);
            if (!existing.Equals(default))
            {
                var stillInside = DistanceSquared(current, existing.Location) <= _proximityPixelsSquared;
                if (!stillInside)
                {
                    isInsideNow = false;
                    leftEvent = new CheckpointLeft(clientId, active, current, DateTimeOffset.UtcNow);
                    // Do not reset currentCheckpoint here (caller keeps last reached until next is entered)
                }
            }
        }

        // Independently, check if we are now within any checkpoint. Choose the nearest within threshold.
        var nearest = FindNearestWithinThreshold(current);
        if (nearest is { } cp)
        {
            isInsideNow = true;
            return cp.Number; // becomes the new CurrentCheckpoint (or remains same number)
        }

        // No new checkpoint entered; keep the old number
        return currentCheckpoint;
    }

    private Checkpoint? FindNearestWithinThreshold(Coordinate pos)
    {
        Checkpoint? best = null;
        double bestDist = double.MaxValue;
        foreach (var cp in _checkpoints)
        {
            var d2 = DistanceSquared(pos, cp.Location);
            if (d2 <= _proximityPixelsSquared && d2 < bestDist)
            {
                best = cp;
                bestDist = d2;
            }
        }
        return best;
    }

    private static double DistanceSquared(Coordinate a, Coordinate b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }
}
```

---

### `ClientWithRuntime` without Subject/IDisposable (event-based, pull method)
```csharp
namespace NamedPipeSync.Common.Application;

using System;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

/// <summary>
/// Thin orchestrator over a <see cref="Client"/> that tracks checkpoint proximity.
/// No Rx, no disposal; raises a CLR event for leave transitions.
/// </summary>
public sealed class ClientWithRuntime
{
    private readonly CheckpointProximityTracker _tracker;
    private bool _isInsideCurrent;

    public ClientWithRuntime(Client client, CheckpointProximityTracker tracker)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));

        // Initialize from current coordinates
        _isInsideCurrent = false;
        CurrentCheckpoint = null;
        OnCoordinate(client.Coordinates);
    }

    public Client Client { get; }

    /// <summary>
    /// The last checkpoint number the client has reached; persists after leaving until a new one is entered.
    /// </summary>
    public int? CurrentCheckpoint { get; private set; }

    /// <summary>
    /// Raised exactly when the client leaves the radius of its current checkpoint.
    /// </summary>
    public event EventHandler<CheckpointLeft>? CheckpointLeft;

    /// <summary>
    /// Feed new coordinates into the runtime logic. Caller decides when to invoke (e.g., upon client updates).
    /// </summary>
    public void OnCoordinate(Coordinate newCoordinates)
    {
        var updated = _tracker.Evaluate(
            Client.Id,
            newCoordinates,
            CurrentCheckpoint,
            _isInsideCurrent,
            out var isInsideNow,
            out var leftEvent);

        _isInsideCurrent = isInsideNow;
        CurrentCheckpoint = updated;

        if (leftEvent is { } evt)
        {
            CheckpointLeft?.Invoke(this, evt);
        }
    }
}
```

Notes
- No `Subject`.
- No `IObservable`.
- No `IDisposable`.
- Consumers can call `OnCoordinate(client.Coordinates)` when coordinates change (e.g., from presentation/application code that already knows when a client moved). This avoids coupling to Rx inside the runtime type.

---

### Optional: Rx adapter (keeps Rx outside of domain/runtime)
If you still need Rx for composition, create a separate adapter that owns the subscription and exposes an `IObservable<CheckpointLeft>`. This adapter can be in the Application layer and is free to depend on `Subject`/`IDisposable`, while `ClientWithRuntime` stays clean.

```csharp
namespace NamedPipeSync.Common.Application;

using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NamedPipeSync.Common.Domain;

/// <summary>
/// Optional Rx adapter: plugs a <see cref="ClientWithRuntime"/> into an Rx pipeline.
/// </summary>
public sealed class ClientRuntimeObservableAdapter : IDisposable
{
    private readonly ClientWithRuntime _runtime;
    private readonly IDisposable _subscription;
    private readonly Subject<CheckpointLeft> _left = new();

    public ClientRuntimeObservableAdapter(ClientWithRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _runtime.CheckpointLeft += OnLeft;

        // If Client exposes an IObservable for coordinates, subscribe here; otherwise, call OnCoordinate from the outside.
        // Example only if you have an observable of coordinates:
        if (_runtime.Client is { } client && client is IHasCoordinateObservable hasObs)
        {
            _subscription = hasObs.CoordinatesObservable
                .Select(e => e.NewCoordinates)
                .Subscribe(c => _runtime.OnCoordinate(c));
        }
        else
        {
            _subscription = System.Reactive.Disposables.Disposable.Empty;
        }
    }

    public IObservable<CheckpointLeft> CheckpointLeft => _left.AsObservable();

    private void OnLeft(object? sender, CheckpointLeft e) => _left.OnNext(e);

    public void Dispose()
    {
        _runtime.CheckpointLeft -= OnLeft;
        _subscription.Dispose();
        _left.Dispose();
    }
}

public interface IHasCoordinateObservable
{
    IObservable<(Coordinate NewCoordinates)> CoordinatesObservable { get; }
}
```

This is just a pattern illustration; the key is that Rx and disposal live here, not in `ClientWithRuntime`.

---

### About the current `Client` using Rx in Domain
Your current `Client` (Domain) references `System.Reactive.Subjects` and exposes `IObservable<CoordinatesChanged>`. Per your own guidelines, the Domain layer should avoid reactive dependencies. Consider one of these:

- Replace with CLR event pattern in `Client`:
  - `public event EventHandler<CoordinatesChanged> CoordinatesChanged;`
  - Raise the event in `SetCoordinates`.
  - This removes the Rx dependency from Domain.

- Or keep Domain minimal (no events) and let Application poll `Client.Coordinates` and generate streams there.

Either option removes `Subject` from Domain and aligns with “entities free of reactive dependencies.”

---

### Result
- `ClientWithRuntime` contains only orchestration logic with no Rx types and no `IDisposable`.
- Pure domain proximity logic is isolated and dependency‑free.
- If needed, Rx is provided by an optional adapter outside the domain/runtime classes.

This meets the requirement: no `Subject` or `IDisposable` in `ClientWithRuntime`, and it preserves the behavior (3‑pixel proximity, leave event, sticky checkpoint value).