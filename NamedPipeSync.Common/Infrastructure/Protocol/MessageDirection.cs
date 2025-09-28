namespace NamedPipeSync.Common.Infrastructure.Protocol;

/// <summary>
///     Marker interface for messages sent by the client and received by the server.
///     Use this to enforce directionality at compile time.
/// </summary>
public interface IClientToServerMessage
{
}

/// <summary>
///     Marker interface for messages sent by the server and received by the client.
///     Use this to enforce directionality at compile time.
/// </summary>
public interface IServerToClientMessage
{
}