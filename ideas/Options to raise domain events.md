# ## The Trade-offs

```csharp
// TODO: this is cheesy, I don't like it.
private readonly Subject<CoordinatesChanged> _coordinatesChanged = new();
```

## The Trade-offs

**Arguments AGAINST Subjects in Domain Entities:**

1. **Infrastructure Dependency**: Domain entities depend on Reactive Extensions (infrastructure concern)
2. **Memory Management**: Subjects need disposal, adding lifecycle complexity to entities
3. **Threading Concerns**: Subjects introduce potential concurrency issues in domain logic
4. **Testability**: Harder to test domain logic when it's coupled to observable infrastructure

**Arguments FOR Subjects in Domain Entities:**

1. **Immediate Event Notification**: Changes are published as they happen
2. **Rich Domain Events**: Entities can express their state changes directly
3. **Composition Friendly**: Easy to compose multiple entity streams
4. **Performance**: Direct event emission without intermediary layers

## Better Alternatives

### Option 1: Domain Events with Event Store Pattern

```csharp
public sealed class ClientWithRuntimeData
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public IReadOnlyList<IDomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();
    
    public void SetCoordinates(Coordinate coords)
    {
        if (!coords.Equals(Coordinates))
        {
            Coordinates = coords;
            _domainEvents.Add(new CoordinatesChanged(Id, coords));
        }
    }
}
```

### Option 2: Repository-Level Event Publishing

```csharp
public sealed class InMemoryClientWithRuntimeDataRepository : IClientWithRuntimeDataRepository
{
    private readonly Subject<CoordinatesChanged> _coordinatesChanged = new();
    
    public IObservable<CoordinatesChanged> CoordinatesChanged => _coordinatesChanged;
    
    public async Task UpsertAsync(ClientWithRuntimeData client, CancellationToken ct)
    {
        // Detect changes and publish events at repository level
        var existing = await TryGetAsync(client.Id, ct);
        if (existing == null || !existing.Coordinates.Equals(client.Coordinates))
        {
            _coordinatesChanged.OnNext(new CoordinatesChanged(client.Id, client.Coordinates));
        }
        
        _map.AddOrUpdate(client.Id.Value, client, (_, __) => client);
    }
}
```

### Option 3: Hybrid Approach (Recommended)

Keep the domain entities pure, but use a domain service to handle event publishing:

```csharp
public sealed class ClientWithRuntimeData
{
    // Pure domain entity - no infrastructure dependencies
    public void SetCoordinates(Coordinate coords)
    {
        if (!coords.Equals(Coordinates))
        {
            var oldCoords = Coordinates;
            Coordinates = coords;
            // Return what changed, let caller decide what to do
        }
    }
}

public sealed class ClientRuntimeService : IClientRuntimeService
{
    private readonly IClientWithRuntimeDataRepository _repo;
    private readonly Subject<CoordinatesChanged> _coordinatesChanged = new();
    
    public IObservable<CoordinatesChanged> CoordinatesChanged => _coordinatesChanged;
    
    public async Task UpdateCoordinatesAsync(ClientId id, Coordinate coords, CancellationToken ct)
    {
        var client = await _repo.TryGetAsync(id, ct);
        if (client != null)
        {
            var oldCoords = client.Coordinates;
            client.SetCoordinates(coords);
            
            if (!oldCoords.Equals(coords))
            {
                _coordinatesChanged.OnNext(new CoordinatesChanged(id, coords));
                await _repo.UpsertAsync(client, ct);
            }
        }
    }
}
```


## Recommendation

I'd suggest **Option 3** because it:
- Keeps domain entities pure and testable
- Centralizes event publishing in application services
- Maintains the reactive patterns you're already using
- Aligns with your DDD layering guidelines

The domain service becomes the "event publisher" while entities remain focused on business rules and state management.