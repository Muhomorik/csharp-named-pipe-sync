namespace NamedPipeSync.Common.Infrastructure.Protocol;

public enum MessageType
{
    ClientSaysHello,
    ServerSendsCoordinate,
    ClientSaysBye,
    ServerRequestsClientClose,
    ServerSendsConfiguration
}
