using System.Windows;

using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Application;

using NLog;

namespace NamedPipeSync.Client.Models;

/// <summary>
///     WPF-backed implementation of <see cref="IApplicationLifetime" />.
///     Sends a polite Bye to the server before shutting down the WPF application.
/// </summary>
public sealed class ApplicationLifetime : IApplicationLifetime
{
    private readonly ILogger _logger;
    private readonly INamedPipeClient _pipeClient;

    public ApplicationLifetime(ILogger logger, INamedPipeClient pipeClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeClient = pipeClient ?? throw new ArgumentNullException(nameof(pipeClient));
    }

    public void Shutdown(int exitCode = 0)
    {
        try
        {
            _pipeClient.SendByeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            try
            {
                _logger.Warn(ex, "Error sending Bye on shutdown");
            }
            catch
            {
            }
        }
        finally
        {
            // If Application.Current is null (e.g., during some design-time scenarios), do nothing.
            Application.Current?.Shutdown(exitCode);
        }
    }
}