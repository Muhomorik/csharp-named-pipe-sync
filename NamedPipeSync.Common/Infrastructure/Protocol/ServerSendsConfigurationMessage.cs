using System;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Application;

namespace NamedPipeSync.Common.Infrastructure.Protocol;

/// <summary>
/// Server-to-client message carrying initial configuration for a client session.
/// Includes the client's starting checkpoint, an optional PNG image encoded as Base64,
/// a ShowMode preset the server wants the client to use, and a UTC timestamp indicating when the configuration was produced.
/// </summary>
public sealed record ServerSendsConfigurationMessage : PipeMessage, IServerToClientMessage
{
    public ServerSendsConfigurationMessage() => Type = MessageType.ServerSendsConfiguration;

    /// <summary>
    /// The starting checkpoint that the client should consider as its initial logical position.
    /// </summary>
    public Checkpoint StartingCheckpoint { get; init; }

    /// <summary>
    /// A PNG image encoded as Base64 for the client to use (e.g., as a background).
    /// Empty string means no image.
    /// </summary>
    public string ScreenshotBase64 { get; init; } = string.Empty;

    /// <summary>
    /// The high-level show/preset mode the server wants the client to apply.
    /// </summary>
    public ShowMode ShowMode { get; init; }

    /// <summary>
    /// UTC timestamp when the configuration was created on the server.
    /// </summary>
    public DateTime TimestampUtc { get; init; }
}
