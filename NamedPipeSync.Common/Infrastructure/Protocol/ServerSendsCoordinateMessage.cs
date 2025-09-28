namespace NamedPipeSync.Common.Infrastructure.Protocol;

public sealed record ServerSendsCoordinateMessage : PipeMessage, IServerToClientMessage
{
    public ServerSendsCoordinateMessage() => Type = MessageType.ServerSendsCoordinate;

    public double X { get; init; }
    public double Y { get; init; }
}