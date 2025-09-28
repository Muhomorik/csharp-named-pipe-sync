## Option 3: Hybrid Domain Events Pattern - General Explanation

### Core Philosophy
This pattern separates **what changed** (domain logic) from **how to notify** (infrastructure concern). Domain entities remain pure and focused on business rules, while application services handle event publishing and orchestration.

### Key Pattern Components

**1. Pure Domain Entities**
- Return boolean flags indicating if state actually changed
- No infrastructure dependencies (no Subjects, no observables)
- Easy to unit test in isolation
- Focus purely on business invariants and state transitions

**2. Application Service Layer** 
- Acts as the "event publisher" and orchestrator
- Owns the reactive Subjects/observables
- Coordinates between domain entities and repositories
- Provides a consistent API for all state changes
- Handles cross-cutting concerns like logging and error handling

**3. Event Dispatchers**
- Aggregate events from multiple sources into unified streams
- Forward events without transformation
- Handle subscription lifecycle management

### Cross-Cutting Concerns Addressed

**Testability**
- Domain entities: Pure functions, no mocking needed
- Application services: Mock repositories easily
- Clear separation of concerns makes each layer testable in isolation

**Memory Management**
- Centralized Subject disposal in services
- No memory leaks from forgotten entity subscriptions
- Clear ownership of reactive resources

**Consistency**
- All state changes go through the same service methods
- Guaranteed event emission for every actual change
- Single place to add cross-cutting logic (audit, validation, etc.)

**Maintainability**
- Domain logic stays in domain entities
- Infrastructure concerns stay in application layer
- Easy to refactor without breaking domain rules

**Performance**
- Lazy event publishing (only when state actually changes)
- No unnecessary event emissions
- Efficient change detection at entity level

### Guidelines Update Suggestions

Add this section to your `guidelines.md`:

```markdown
## Domain Events Pattern

### Preferred Approach: Hybrid Domain Events

Domain entities should remain pure and infrastructure-free:
- Return boolean indicators when state changes occur
- No direct event publishing (no Subjects, IObservable, etc.)
- Focus on business rules and invariants only

Application services handle event orchestration:
- Own reactive Subjects and observables
- Coordinate between entities and repositories  
- Provide consistent APIs for all state mutations
- Handle cross-cutting concerns (logging, validation, etc.)

Example pattern:
```
csharp
// Domain Entity (Pure)
public bool SetCoordinates(Coordinate coords)
{
    if (!coords.Equals(Coordinates))
    {
        Coordinates = coords;
        return true; // Changed
    }
    return false; // No change
}

// Application Service (Event Publisher)  
public async Task UpdateCoordinatesAsync(ClientId id, Coordinate coords)
{
    var entity = await GetOrCreateAsync(id);
    if (entity.SetCoordinates(coords))
    {
        await _repository.UpsertAsync(entity);
        _coordinatesChanged.OnNext(new CoordinatesChanged(id, coords));
    }
}
```
### Benefits
- **Testability**: Pure domain entities, mockable services
- **Memory Management**: Centralized reactive resource ownership
- **Consistency**: Single API for all state changes with guaranteed events
- **Maintainability**: Clear separation between business logic and infrastructure
```


### Why This Pattern Works Well for Your Architecture

**DDD Alignment**: Keeps domain pure, infrastructure in proper layers
**Reactive Compatibility**: Maintains your existing observable streams
**Performance**: Only publishes events when state actually changes
**Scalability**: Easy to add new event types or cross-cutting concerns
**Error Handling**: Centralized exception handling in services

This pattern essentially treats domain entities as "value objects with behavior" and application services as "event-aware coordinators" - giving you the best of both worlds without the architectural compromises.