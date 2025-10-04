using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Domain.Events;
using NamedPipeSync.Common.Infrastructure.Protocol;
using NamedPipeSync.Server.Services;
using NLog;

namespace NamedPipeSync.Server.Models;

/// <summary>
///     SERVER. Default implementation of <see cref="IMainWindowServerModel"/>.
///     Contains non-UI orchestration used by the Server Main Window.
/// </summary>
public sealed class MainWindowServerModel : IMainWindowServerModel
{
    private readonly ILogger _logger;
    private readonly INamedPipeServer _server;
    private readonly IClientWithRuntimeRepository _repository;
    private readonly IClientWithRuntimeEventDispatcher _events;
    private readonly IClientProcessLauncher _launcher;

    public IObservable<ClientConnectionChange> ConnectionChanged => _server.ConnectionChanged;
    public IObservable<ClientWithRuntimeEvent> Events => _events.Events;

    /// <summary>
    ///     DI constructor. Keep non-UI and do not terminate process here.
    /// </summary>
    /// <param name="logger">NLog logger resolved from DI. Must not be null.</param>
    /// <param name="server">Named pipe server instance. Must not be null.</param>
    /// <param name="repository">Runtime repository for clients. Must not be null.</param>
    /// <param name="events">Domain event dispatcher. Must not be null.</param>
    /// <param name="launcher">Client process launcher. Must not be null.</param>
    [UsedImplicitly]
    public MainWindowServerModel(
        ILogger logger,
        INamedPipeServer server,
        IClientWithRuntimeRepository repository,
        IClientWithRuntimeEventDispatcher events,
        IClientProcessLauncher launcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public void StartServer() => _server.Start();

    public void SeedMissingClients()
    {
        foreach (var cp in Checkpoints.Start)
        {
            var id = new ClientId(cp.Id);
            if (!_repository.TryGet(id, out var _))
            {
                var client = new ClientWithRuntime(id, cp)
                {
                    Coordinates = cp.Location,
                    Connection = ConnectionState.Disconnected,
                    IsOnCheckpoint = true
                };
                _repository.Update(client);
            }
        }
    }

    public void EnsureClientEntryOnConnectionChange(ClientId clientId, ConnectionState state)
    {
        if (!_repository.TryGet(clientId, out var client))
        {
            // Seed using checkpoint with same id when available, otherwise use first checkpoint
            var cp = Checkpoints.Start.FirstOrDefault(c => c.Id == clientId.Id);
            if (cp.Id == 0 && Checkpoints.Start.Count > 0)
            {
                cp = Checkpoints.Start[0];
            }
            client = new ClientWithRuntime(clientId, cp);
        }

        client!.Connection = state;
        _repository.Update(client);
    }

    public IReadOnlyCollection<ClientWithRuntime> GetAllClients() => _repository.GetAll();
    public bool TryGet(ClientId clientId, out ClientWithRuntime? client) => _repository.TryGet(clientId, out client);

    public void StartClient(ClientId id) => _launcher.StartClient(id);

    public Task StartManyAsync(IEnumerable<ClientId> ids, CancellationToken ct = default) => _launcher.StartManyAsync(ids, ct);

    public async Task CloseAllClientsAsync(CancellationToken ct = default)
    {
        try
        {
            var ids = _server.ConnectedClientIds;
            var tasks = ids.Select(id => _server.SendCloseRequestAsync(id, ct));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
        }
    }
}
