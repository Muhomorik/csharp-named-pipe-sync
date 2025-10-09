using System;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Application;

/// <summary>
/// Provides an application-level configuration payload that the server should send to a client
/// immediately after a successful handshake (<see cref="Protocol.ClientHelloMessage"/>).
/// Implementations must be UI-agnostic and must not perform process termination.
/// </summary>
public interface IServerConfigurationProvider
{
    /// <summary>
    /// Builds a configuration message for the specified client.
    /// </summary>
    /// <param name="clientId">The logical client identifier.</param>
    /// <returns>A fully populated <see cref="ServerSendsConfigurationMessage"/> ready to be sent.</returns>
    ServerSendsConfigurationMessage BuildConfigurationFor(ClientId clientId);
}
