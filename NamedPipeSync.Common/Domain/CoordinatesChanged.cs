using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Domain;

public readonly record struct CoordinatesChanged(ClientId ClientId, Coordinate NewCoordinates);