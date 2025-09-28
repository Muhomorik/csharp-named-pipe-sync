namespace NamedPipeSync.Common.Infrastructure.Protocol;

public sealed record ClientByeMessage : PipeMessage, IClientToServerMessage
{
    public ClientByeMessage() => Type = MessageType.ClientSaysBye;
}