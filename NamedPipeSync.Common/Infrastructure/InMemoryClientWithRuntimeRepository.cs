using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;
using NLog;

namespace NamedPipeSync.Common.Infrastructure;

/// <summary>
/// In-memory implementation of <see cref="IClientWithRuntimeRepository"/>.
/// Thread-safe and suitable for process-scoped runtime state. Synchronous and event-free.
/// </summary>
public sealed class InMemoryClientWithRuntimeRepository : IClientWithRuntimeRepository
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, ClientWithRuntime> _map = new();

    public InMemoryClientWithRuntimeRepository(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<ClientWithRuntime> GetAll()
    {
        // If the map is empty, seed it from the configured checkpoints so callers
        // (e.g. the server model) don't need to duplicate checkpoint seeding logic.
        if (_map.IsEmpty)
        {
            foreach (var cp in Checkpoints.Start)
            {
                var id = new ClientId(cp.Id);
                var client = new ClientWithRuntime(id, cp)
                {
                    Coordinates = cp.Location,
                    Connection = ConnectionState.Disconnected,
                    IsOnCheckpoint = true
                };
                _map.TryAdd(id.Id, client);
            }
        }

        // Materialize to an array to provide a stable snapshot
        var snapshot = _map.Values.ToArray();
        if (_logger.IsTraceEnabled)
        {
            _logger.Trace("GetAll snapshotCount={0}", snapshot.Length);
        }
        return snapshot;
    }

    public bool TryGet(ClientId id, out ClientWithRuntime? client)
    {
        var found = _map.TryGetValue(id.Id, out client);
        if (_logger.IsTraceEnabled)
        {
            _logger.Trace("TryGet id={0} found={1}", id.Id, found);
        }
        return found;
    }

    public bool Remove(ClientId id)
    {
        var removed = _map.TryRemove(id.Id, out _);
        if (_logger.IsTraceEnabled)
        {
            _logger.Trace("Remove id={0} removed={1}", id.Id, removed);
        }
        return removed;
    }

    public void Update(ClientWithRuntime client)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        var key = client.Id.Id;
        var existed = _map.ContainsKey(key);
        _map.AddOrUpdate(key, client, (_, __) => client);
        if (_logger.IsTraceEnabled)
        {
            _logger.Trace("Update id={0} action={1}", key, existed ? "Replace" : "Add");
        }
    }
}
