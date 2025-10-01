using System;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Domain.Events;

namespace NamedPipeSync.Common.Application;

public sealed class SimpleRingCoordinatesCalculator : ICoordinatesCalculator
{
    private readonly IClientWithRuntimeEventDispatcher _events;

    public SimpleRingCoordinatesCalculator(IClientWithRuntimeEventDispatcher events)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public Coordinate NextCoordinates(DateTimeOffset now, ClientWithRuntime client, int stepPixels)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (stepPixels < 0) throw new ArgumentOutOfRangeException(nameof(stepPixels));

        // Initialize position on first call if not set
        if (client.InitializePositionIfNeeded())
        {
            // Publish initial coordinates
            _events.Publish(CoordinatesUpdated.Create(client.Id, client.Coordinates));
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
            // Arrived at the target checkpoint
            client.ArriveAt(target);
            _events.Publish(CoordinatesUpdated.Create(client.Id, client.Coordinates));
            _events.Publish(CheckpointReached.Create(client.Id, target));
            return client.Coordinates;
        }
        // Move step towards target
        var next = client.MoveStepTowards(target, stepPixels);
        _events.Publish(CoordinatesUpdated.Create(client.Id, next));
        return next;
    }
}
