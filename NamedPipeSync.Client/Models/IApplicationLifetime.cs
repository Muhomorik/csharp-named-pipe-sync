namespace NamedPipeSync.Client.Models;

/// <summary>
///     Application lifetime abstraction to allow ViewModels to request application shutdown
///     without referencing WPF infrastructure directly (DDD-friendly).
/// </summary>
public interface IApplicationLifetime
{
    /// <summary>
    ///     Requests application shutdown.
    /// </summary>
    /// <param name="exitCode">Optional exit code. Default is 0 (success).</param>
    void Shutdown(int exitCode = 0);
}