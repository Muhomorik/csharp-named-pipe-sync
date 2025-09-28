namespace NamedPipeSync.Common.Application;

/// <summary>
///     Represents a client-side connection state change (to the server).
/// </summary>
/// <param name="State">The new connection state.</param>
public readonly record struct ClientConnectionStateChange(ConnectionState State);