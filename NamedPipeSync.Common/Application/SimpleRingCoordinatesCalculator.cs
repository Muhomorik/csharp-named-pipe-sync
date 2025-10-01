using System;
using NamedPipeSync.Common.Domain;

namespace NamedPipeSync.Common.Application;

public sealed class SimpleRingCoordinatesCalculator : ICoordinatesCalculator
{
    public Coordinate NextCoordinates(DateTimeOffset now, ClientWithRuntime client, int stepPixels)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (stepPixels < 0) throw new ArgumentOutOfRangeException(nameof(stepPixels));

        // Initialize position on first call if not set
        if (client.InitializePositionIfNeeded())
        {
            return client.Coordinates;
        }

        // Determine current target checkpoint
        var target = client.CurrentCheckpoint;
        var current = client.Coordinates;
        var dx = target.Location.X - current.X;
        var dy = target.Location.Y - current.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance <= stepPixels || distance == 0)
        {
            // Arrived at target checkpoint
            client.ArriveAt(target);
            return client.Coordinates;
        }
        else
        {
            // Move step towards target
            return client.MoveStepTowards(target, stepPixels);
        }
    }
}
