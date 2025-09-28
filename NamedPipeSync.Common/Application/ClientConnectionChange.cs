using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Application;

/// <summary>
///     Represents a change in a client's connection state.
/// </summary>
/// <param name="ClientId">The client identifier.</param>
/// <param name="State">The new state for the client.</param>
public readonly record struct ClientConnectionChange(ClientId ClientId, ConnectionState State);