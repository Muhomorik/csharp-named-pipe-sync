using System.Text.Json;
using System.Text.Json.Serialization;

namespace NamedPipeSync.Common.Infrastructure.Protocol;

/// <summary>
///     Centralizes protocol-wide constants and JSON serialization settings used by the
///     Named Pipe IPC layer.
/// </summary>
/// <remarks>
///     Purpose and scope:
///     - The values defined here must be treated as a contract between the server and all clients.
///     - Changing any value (e.g., the pipe name or JSON options) impacts interoperability and should
///     be considered a breaking change unless all parties are updated in lockstep.
///     Discovery pipe name:
///     - <see cref="DiscoveryPipeName" /> is the well-known name both clients and the server use to
///     discover and establish an IPC connection.
///     - It is intentionally short and without path qualifiers; see .NET NamedPipeServerStream/NamedPipeClientStream
///     documentation for platform-specific naming rules and security considerations.
///     JSON configuration:
///     - <see cref="JsonOptions" /> is shared by all protocol messages to ensure consistent casing,
///     enum handling, and omission of null values.
///     - Messages use a discriminated-union style with a top-level "type" field (see <see cref="MessageType" />),
///     and are serialized/deserialized with these options via <see cref="PipeSerialization" />.
///     Threading/immutability:
///     - <see cref="JsonOptions" /> is created once and treated as read-only. Do not mutate it at runtime.
///     - Access to these members is thread-safe for reading.
/// </remarks>
public static class PipeProtocol
{
    /// <summary>
    ///     The well-known Named Pipe name that the server listens on and clients connect to for discovery
    ///     and message exchange.
    /// </summary>
    /// <remarks>
    ///     - Used by <see cref="System.IO.Pipes.NamedPipeServerStream" /> on the server and
    ///     <see cref="System.IO.Pipes.NamedPipeClientStream" /> on clients.
    ///     - Must be identical on both sides. Changing this requires updating all participants.
    ///     - Keep names ASCII and short to avoid platform-specific limitations.
    /// </remarks>
    public const string DiscoveryPipeName = "NamedPipeSync_Discovery";

    /// <summary>
    ///     The shared JSON serializer configuration for all protocol messages.
    /// </summary>
    /// <value>
    ///     Configured with:
    ///     - PropertyNamingPolicy = <see cref="JsonNamingPolicy.CamelCase" />
    ///     - DefaultIgnoreCondition = <see cref="JsonIgnoreCondition.WhenWritingNull" />
    ///     - WriteIndented = <see langword="false" />
    ///     - Converters: <see cref="JsonStringEnumConverter" /> with camelCase naming
    /// </value>
    /// <remarks>
    ///     - Treat this instance as immutable and do not modify converters or policies at runtime.
    ///     - Used by <see cref="PipeSerialization" /> for both serialization and deserialization.
    ///     - Aligning enum string casing with camelCase ensures consistent payloads across platforms and languages.
    /// </remarks>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}