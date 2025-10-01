using System.Diagnostics;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Domain.Events;

/// <summary>
/// Domain event indicating that a client's coordinates have changed.
/// </summary>
[DebuggerDisplay("CoordinatesChanged: {ClientId} -> {NewCoordinates}")]
public sealed record CoordinatesChanged : ClientWithRuntimeEvent
{
    /// <summary>
    /// New coordinates of the client.
    /// </summary>
    public Coordinate NewCoordinates { get; init; }

    /// <summary>
    /// Optional previous coordinates, if known by the emitter.
    /// </summary>
    public Coordinate? PreviousCoordinates { get; init; }

    /// <summary>
    /// Convenience factory.
    /// </summary>
    public static CoordinatesChanged Create(ClientId clientId, Coordinate newCoords, Coordinate? previous = null)
        => new() { ClientId = clientId, NewCoordinates = newCoords, PreviousCoordinates = previous };
}
