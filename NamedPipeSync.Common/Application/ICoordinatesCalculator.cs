using System;
using NamedPipeSync.Common.Domain;

namespace NamedPipeSync.Common.Application;

public interface ICoordinatesCalculator
{
    /// <summary>
    /// Calculates the next coordinate for the specified client based on the current time and movement step.
    /// The implementation should update the client's runtime state to reflect movement towards the next checkpoint.
    /// </summary>
    /// <param name="now">Current timestamp used for time-based strategies. Not required for checkpoint stepping but kept for extensibility.</param>
    /// <param name="client">The client with runtime state; must not be null.</param>
    /// <param name="stepPixels">Movement step in pixels; must be greater than or equal to 0.</param>
    /// <returns>The updated coordinate for the client.</returns>
    Coordinate NextCoordinates(DateTimeOffset now, ClientWithRuntime client, int stepPixels);
}
