namespace NamedPipeSync.Common.Infrastructure.Protocol;

/// <summary>
///     Client-to-server handshake message that must be sent immediately after a client connects to the
///     discovery named pipe. The server uses this message to associate the transport with a logical client
///     identified by its client identifier.
/// </summary>
/// <remarks>
///     Usage
///     - Client: <see cref="NamedPipeSyncCommon.Infrastructure.NamedPipeClient.ConnectAsync(System.TimeSpan?,System.Threading.CancellationToken)"/>
///       creates and sends a Hello message as the very first line via <see cref="PipeSerialization.SerializeFromClient(object)"/>.
///       The client sets the numeric client identifier on the message (taken from <see cref="NamedPipeSyncCommon.Application.ClientId"/>).
///     - Server: <see cref="NamedPipeSyncCommon.Infrastructure.NamedPipeServer"/> reads the first line and
///       expects it to be a Hello message inside its accept loop. If received, it registers the connection
///       for that client identifier; any other first message is ignored and the connection is dropped.
///
///     The <see cref="NamedPipeSyncCommon.Infrastructure.Protocol.PipeMessage.Type"/> is set to
///     <see cref="MessageType.ClientSaysHello"/> by the constructor and should not be changed by callers.
/// </remarks>
/// <seealso cref="IClientToServerMessage"/>
public sealed record ClientHelloMessage : PipeMessage, IClientToServerMessage
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ClientHelloMessage"/> class and sets its
    ///     <see cref="PipeMessage.Type"/> to
    ///     <see cref="MessageType.ClientSaysHello"/>.
    /// </summary>
    public ClientHelloMessage() => Type = MessageType.ClientSaysHello;
}