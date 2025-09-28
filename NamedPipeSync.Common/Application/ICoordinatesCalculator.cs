using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Application;

public interface ICoordinatesCalculator
{
    Coordinate NextCoordinates(ClientId me, IReadOnlyDictionary<ClientId, Coordinate> all);
}