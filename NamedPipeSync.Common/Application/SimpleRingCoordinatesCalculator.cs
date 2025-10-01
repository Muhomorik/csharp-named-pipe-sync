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
        if (client.Coordinates.X == 0 && client.Coordinates.Y == 0)
        {
            client.Coordinates = client.LastCheckpoint.Location;
            client.IsOnCheckpoint = true;
            return client.Coordinates;
        }

        // Determine current target checkpoint
        var target = client.IsOnCheckpoint ? client.LastCheckpoint : client.MovingToCheckpoint;
        var current = client.Coordinates;
        var dx = target.Location.X - current.X;
        var dy = target.Location.Y - current.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance <= stepPixels || distance == 0)
        {
            // Arrived at target checkpoint
            client.Coordinates = target.Location;
            client.LastCheckpoint = target;
            client.IsOnCheckpoint = true;

            // Set next target checkpoint (wrap around)
            var cps = Checkpoints.Start;
            var idx = Math.Max(0, cps.ToList().FindIndex(cp => cp.Id == target.Id));
            var nextIdx = (idx + 1) % cps.Count;
            client.MovingToCheckpoint = cps[nextIdx];

            return client.Coordinates;
        }
        else
        {
            // Move step towards target
            var step = Math.Min(stepPixels, distance);
            var nx = current.X + step * (dx / distance);
            var ny = current.Y + step * (dy / distance);
            var next = new Coordinate(nx, ny);
            client.Coordinates = next;
            client.IsOnCheckpoint = false;
            return next;
        }
    }
}
