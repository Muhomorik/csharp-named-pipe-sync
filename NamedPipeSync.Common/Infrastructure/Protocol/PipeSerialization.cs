using System.Text.Json;

namespace NamedPipeSync.Common.Infrastructure.Protocol;

/// <summary>
///     Provides JSON serialization and deserialization helpers for pipe messages exchanged
///     between the server and clients.
/// </summary>
/// <remarks>
///     - Serialization uses <see cref="PipeProtocol.JsonOptions" /> to ensure consistent casing,
///     enum handling, and other JSON settings across the application.
///     - Deserialization is tolerant to malformed input: it returns <see langword="null" /> on
///     invalid JSON, when the required <c>type</c> discriminator is missing/empty, or when the
///     message type is unknown.
///     - The JSON format uses a simple discriminated-union pattern via a top-level <c>type</c>
///     property whose value must map to <see cref="MessageType" />.
/// </remarks>
public static class PipeSerialization
{
    /// <summary>
    ///     Serializes a concrete <see cref="PipeMessage" /> instance into a JSON string suitable for sending over the pipe.
    /// </summary>
    /// <typeparam name="T">A concrete message type that derives from <see cref="PipeMessage" />.</typeparam>
    /// <param name="message">The message instance to serialize. Expected to be non-null.</param>
    /// <returns>A JSON string representation of <paramref name="message" />.</returns>
    /// <exception cref="System.NotSupportedException">
    ///     Thrown if the runtime type of <paramref name="message" /> is not
    ///     supported by the serializer.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown if an error occurs during serialization.</exception>
    /// <exception cref="System.OutOfMemoryException">Thrown if there is not enough memory to create the JSON string.</exception>
    /// <remarks>
    ///     - Uses <see cref="PipeProtocol.JsonOptions" /> to match the protocol's JSON configuration.
    ///     - The caller is responsible for appending a newline when sending over line-delimited pipes.
    /// </remarks>
    public static string Serialize<T>(T message) where T : PipeMessage =>
        JsonSerializer.Serialize(message, PipeProtocol.JsonOptions);

    /// <summary>
    ///     Serializes a message that is allowed to be sent by the client to the server.
    ///     Using this overload enforces directionality at compile time.
    /// </summary>
    public static string SerializeFromClient<T>(T message) where T : PipeMessage, IClientToServerMessage =>
        JsonSerializer.Serialize(message, PipeProtocol.JsonOptions);

    /// <summary>
    ///     Convenience overload that accepts the direction marker interface.
    /// </summary>
    public static string SerializeFromClient(IClientToServerMessage message) =>
        JsonSerializer.Serialize((object)message, PipeProtocol.JsonOptions);

    /// <summary>
    ///     Serializes a message that is allowed to be sent by the server to the client.
    ///     Using this overload enforces directionality at compile time.
    /// </summary>
    public static string SerializeFromServer<T>(T message) where T : PipeMessage, IServerToClientMessage =>
        JsonSerializer.Serialize(message, PipeProtocol.JsonOptions);

    /// <summary>
    ///     Convenience overload that accepts the direction marker interface.
    /// </summary>
    public static string SerializeFromServer(IServerToClientMessage message) =>
        JsonSerializer.Serialize((object)message, PipeProtocol.JsonOptions);

    /// <summary>
    ///     Deserializes a JSON string into a concrete <see cref="PipeMessage" /> based on the <c>type</c> discriminator.
    /// </summary>
    /// <param name="json">The JSON payload to parse. Must be a complete JSON object string.</param>
    /// <returns>
    ///     A concrete <see cref="PipeMessage" /> (e.g., <see cref="ClientHelloMessage" />, <see cref="ClientByeMessage" />,
    ///     <see cref="ServerSendsCoordinateMessage" />) when successful; otherwise <see langword="null" /> for malformed JSON,
    ///     missing/unknown <c>type</c>, or any parse/validation errors.
    /// </returns>
    /// <remarks>
    ///     - The <c>type</c> field is parsed case-insensitively as a <see cref="MessageType" />.
    ///     - All exceptions during parsing are intentionally swallowed and result in <see langword="null" /> to make
    ///     the pipe reading loops resilient to malformed or partial input.
    ///     - Unknown message types are ignored (return <see langword="null" />).
    /// </remarks>
    public static PipeMessage? Deserialize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeElement))
            {
                return null;
            }

            var typeStr = typeElement.GetString();
            if (string.IsNullOrWhiteSpace(typeStr))
            {
                return null;
            }

            if (Enum.TryParse<MessageType>(typeStr, true, out var mt))
            {
                return mt switch
                {
                    MessageType.ClientSaysHello => JsonSerializer.Deserialize<ClientHelloMessage>(json,
                        PipeProtocol.JsonOptions),
                    MessageType.ClientSaysBye => JsonSerializer.Deserialize<ClientByeMessage>(json,
                        PipeProtocol.JsonOptions),
                    MessageType.ServerSendsCoordinate => JsonSerializer.Deserialize<ServerSendsCoordinateMessage>(json,
                        PipeProtocol.JsonOptions),
                    MessageType.ServerRequestsClientClose => JsonSerializer
                        .Deserialize<ServerRequestsClientCloseMessage>(json,
                            PipeProtocol.JsonOptions),
                    _ => null
                };
            }
        }
        catch
        {
            // ignore malformed
        }

        return null;
    }

    /// <summary>
    ///     Deserializes JSON that the server receives from clients. Only client->server messages are returned.
    /// </summary>
    public static IClientToServerMessage? DeserializeForServer(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeElement))
            {
                return null;
            }

            var typeStr = typeElement.GetString();
            if (string.IsNullOrWhiteSpace(typeStr))
            {
                return null;
            }

            if (!Enum.TryParse<MessageType>(typeStr, true, out var mt))
            {
                return null;
            }

            return mt switch
            {
                MessageType.ClientSaysHello => JsonSerializer.Deserialize<ClientHelloMessage>(json,
                    PipeProtocol.JsonOptions),
                MessageType.ClientSaysBye => JsonSerializer.Deserialize<ClientByeMessage>(json,
                    PipeProtocol.JsonOptions),
                _ => null // ignore server-to-client types
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Deserializes JSON that the client receives from the server. Only server->client messages are returned.
    /// </summary>
    public static IServerToClientMessage? DeserializeForClient(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeElement))
            {
                return null;
            }

            var typeStr = typeElement.GetString();
            if (string.IsNullOrWhiteSpace(typeStr))
            {
                return null;
            }

            if (!Enum.TryParse<MessageType>(typeStr, true, out var mt))
            {
                return null;
            }

            return mt switch
            {
                MessageType.ServerSendsCoordinate => JsonSerializer.Deserialize<ServerSendsCoordinateMessage>(json,
                    PipeProtocol.JsonOptions),
                MessageType.ServerRequestsClientClose => JsonSerializer.Deserialize<ServerRequestsClientCloseMessage>(
                    json, PipeProtocol.JsonOptions),
                _ => null // ignore client-to-server types
            };
        }
        catch
        {
            return null;
        }
    }
}