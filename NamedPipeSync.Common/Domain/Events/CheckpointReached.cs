using System.Diagnostics;

using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Domain.Events;

/// <summary>
/// Domain event indicating that a client has reached a checkpoint.
/// </summary>
[DebuggerDisplay("CheckpointReached: {ClientId} -> #{Checkpoint.Id} at {Checkpoint.Location}")]
public sealed record CheckpointReached : ClientWithRuntimeEvent
{
    /// <summary>
    /// The checkpoint that has been reached.
    /// </summary>
    public Checkpoint Checkpoint { get; init; }

    /// <summary>
    /// Convenience factory.
    /// </summary>
    public static CheckpointReached Create(ClientId clientId, Checkpoint checkpoint)
        => new() { ClientId = clientId, Checkpoint = checkpoint };
}
