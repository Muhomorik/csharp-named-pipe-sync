using System.Diagnostics;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Domain.Events;

/// <summary>
/// Domain event indicating that a client's coordinates have been updated.
/// </summary>
[DebuggerDisplay("CoordinatesUpdated: {ClientId} -> ({Coordinates.X}, {Coordinates.Y})")]
public sealed record CoordinatesUpdated : ClientWithRuntimeEvent
{
    /// <summary>
    /// The new coordinates of the client after the update.
    /// </summary>
    public Coordinate Coordinates { get; init; }

    /// <summary>
    /// Convenience factory.
    /// </summary>
    public static CoordinatesUpdated Create(ClientId clientId, Coordinate coordinates)
        => new() { ClientId = clientId, Coordinates = coordinates };
}
