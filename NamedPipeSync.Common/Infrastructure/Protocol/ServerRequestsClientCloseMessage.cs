namespace NamedPipeSync.Common.Infrastructure.Protocol;

public sealed record ServerRequestsClientCloseMessage : PipeMessage, IServerToClientMessage
{
    public ServerRequestsClientCloseMessage() => Type = MessageType.ServerRequestsClientClose;
}