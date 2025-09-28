using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Application;

public sealed class SimpleRingCoordinatesCalculator : ICoordinatesCalculator
{
    private const double Radius = 100.0;

    public Coordinate NextCoordinates(ClientId me, IReadOnlyDictionary<ClientId, Coordinate> all)
    {
        var ordered = all.Keys.OrderBy(k => k.Id).ToList();
        var count = ordered.Count == 0 ? 1 : ordered.Count;
        var index = Math.Max(0, ordered.FindIndex(k => k.Equals(me)));
        var angle = 2 * Math.PI * (index / (double)count);
        var x = Radius * Math.Cos(angle);
        var y = Radius * Math.Sin(angle);
        return new Coordinate(x, y);
    }
}