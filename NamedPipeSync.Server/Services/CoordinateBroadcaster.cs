using System.Reactive.Concurrency;
using System.Reactive.Linq;

using NamedPipeSync.Common.Application;

namespace NamedPipeSync.Server.Services;

public sealed class CoordinateBroadcaster : ICoordinateBroadcaster
{
    private readonly ICoordinatesCalculator _calculator;
    private readonly IScheduler _scheduler;
    private readonly INamedPipeServer _server;

    private IDisposable? _subscription;

    public CoordinateBroadcaster(
        INamedPipeServer server,
        ICoordinatesCalculator calculator,
        IScheduler scheduler)
    {
        _server = server;
        _calculator = calculator;
        _scheduler = scheduler;
    }

    public IDisposable Start()
    {
        _subscription?.Dispose();
        _subscription = Observable
            .Interval(TimeSpan.FromSeconds(10), _scheduler)
            .Subscribe(async _ => await BroadcastOnce());
        return _subscription;
    }

    public void Dispose() => _subscription?.Dispose();

    private async Task BroadcastOnce()
    {
        // TODO: implement
        await Task.Yield();
        var snapshotIds = _server.ConnectedClientIds;
        // var all = _currentCoords();
        //
        // foreach (var id in snapshotIds)
        // {
        //     var next = _calculator.NextCoordinates(id, all);
        //     await _server.SendCoordinateAsync(id, next.X, next.Y);
        // }
    }
}