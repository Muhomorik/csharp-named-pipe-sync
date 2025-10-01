using System;
using System.Diagnostics;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Domain.Events;

/// <summary>
/// Base type for all domain events emitted for the ClientWithRuntime aggregate.
/// Events are immutable intent messages without any UI or infrastructure concerns.
/// </summary>
/// <remarks>
/// - Belongs to the Domain layer; free of reactive or UI dependencies.
/// - Used by the Application layer to compose observables and orchestrate use-cases.
/// </remarks>
[DebuggerDisplay("{GetType().Name} for {ClientId} at {OccurredAt:O}")]
public abstract record ClientWithRuntimeEvent
{
    /// <summary>
    /// Unique identifier of the client this event relates to.
    /// </summary>
    public ClientId ClientId { get; init; }

    /// <summary>
    /// Timestamp when the event occurred (UTC recommended).
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
