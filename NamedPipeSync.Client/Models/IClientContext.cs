namespace NamedPipeSync.Client.Models;

/// <summary>
///     Application-level abstraction that exposes contextual information about the running client.
///     In DDD terms, this belongs to the application layer (composition/infrastructure boundary),
///     allowing the UI and domain to depend on an abstraction rather than a specific CLI/options type.
/// </summary>
/// <remarks>
///     Purpose:
///     - Decouple presentation/domain code from the CommandLine parsing library (infrastructure).
///     - Provide a stable contract for accessing client context (e.g., ClientId) regardless of source
///     (CLI, config, environment, test doubles).
/// </remarks>
public interface IClientContext
{
    /// <summary>
    ///     The identifier of the client as interpreted by the application.
    /// </summary>
    int ClientId { get; }
}