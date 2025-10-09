using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;

namespace NamedPipeSync.Client.Models;

/// <summary>
/// Model implementation providing encapsulated MainWindow functionality
/// </summary>
public class MainWindowModel : IMainWindowModel
{
    private readonly IClientContext _clientContext;
    private readonly IApplicationLifetime _appLifetime;
    private readonly INamedPipeClient _pipeClient;

    /// <summary>
    /// Initializes a new instance of the MainWindowModel class.
    /// </summary>
    /// <param name="clientContext">Client context containing client-specific information</param>
    /// <param name="appLifetime">Application lifetime service for managing application shutdown</param>
    /// <param name="pipeClient">Named pipe client for server communication</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
    public MainWindowModel(
        IClientContext clientContext,
        IApplicationLifetime appLifetime,
        INamedPipeClient pipeClient)
    {
        _clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
        _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
        _pipeClient = pipeClient ?? throw new ArgumentNullException(nameof(pipeClient));
    }

    /// <inheritdoc />
    public int GetClientId() => _clientContext.ClientId;

    /// <inheritdoc />
    public void RequestShutdown() => _appLifetime.Shutdown();

    /// <inheritdoc />
    public async Task ConnectAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (timeout.HasValue)
            await _pipeClient.ConnectAsync(timeout.Value, cancellationToken);
        else
            await _pipeClient.ConnectAsync();
    }

    /// <inheritdoc />
    public async Task DisconnectAsync() => await _pipeClient.DisconnectAsync();

    /// <inheritdoc />
    public IObservable<ClientConnectionStateChange> ConnectionChanges => _pipeClient.ConnectionChanged;

    /// <inheritdoc />
    public IObservable<Coordinate> Coordinates => _pipeClient.Coordinates;

    /// <inheritdoc />
    public IObservable<NamedPipeSync.Common.Infrastructure.Protocol.ServerSendsConfigurationMessage> ConfigurationReceived => _pipeClient.ConfigurationReceived;
}
