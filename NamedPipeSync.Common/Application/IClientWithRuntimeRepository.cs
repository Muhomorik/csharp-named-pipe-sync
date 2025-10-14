using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Application;

/// <summary>
/// Repository for ephemeral runtime client state (<see cref="ClientWithRuntime"/>).
/// Implementations are typically in-memory and scoped to the hosting process lifetime.
/// Not intended for durable persistence. This contract is synchronous and event-free.
/// </summary>
public interface IClientWithRuntimeRepository
{
    /// <summary>
    /// Returns a materialized snapshot of all clients at the time of the call.
    /// </summary>
    IReadOnlyList<ClientWithRuntime> GetAll();

    /// <summary>
    /// Attempts to get a client by id.
    /// </summary>
    /// <param name="id">Client identifier.</param>
    /// <param name="client">When this method returns, contains the client if found; otherwise null.</param>
    /// <returns>true if found; otherwise false.</returns>
    bool TryGet(ClientId id, out ClientWithRuntime? client);

    /// <summary>
    /// Removes the client with the specified id.
    /// </summary>
    /// <param name="id">Client identifier to remove.</param>
    /// <returns>true if the client existed and was removed; otherwise false.</returns>
    bool Remove(ClientId id);

    /// <summary>
    /// Inserts a new client or replaces the existing one with the same id.
    /// </summary>
    /// <param name="client">Client runtime entity to upsert. Must not be null.</param>
    void Update(ClientWithRuntime client);
}
