namespace NamedPipeSync.Client.Models;

/// <summary>
///     Default implementation that adapts parsed CLI options into the application-level <see cref="IClientContext" />.
/// </summary>
/// <remarks>
///     Why needed:
///     - Maps infrastructure type (CliClientOptions) into a stable abstraction for the rest of the app (IClientContext).
///     - Prevents CommandLine library from leaking into UI/domain.
///     - Keeps a single source of truth for contextual values populated at composition time.
/// </remarks>
public sealed class ClientContext : IClientContext
{
    /// <summary>
    ///     Creates a context for the running client from parsed CLI options.
    /// </summary>
    /// <param name="options">Parsed CLI options (infrastructure detail).</param>
    /// <exception cref="ArgumentNullException">When <paramref name="options" /> is null.</exception>
    public ClientContext(CliClientOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        ClientId = options.Id;
    }

    /// <inheritdoc />
    public int ClientId { get; }
}