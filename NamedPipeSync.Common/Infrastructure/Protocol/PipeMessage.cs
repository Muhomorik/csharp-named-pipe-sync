namespace NamedPipeSync.Common.Infrastructure.Protocol;

public record PipeMessage
{
    public MessageType Type { get; init; }
    public int ClientId { get; init; }
}