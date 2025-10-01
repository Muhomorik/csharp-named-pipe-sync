using System.Collections.Concurrent;

using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Server.Services;

// Repository of clients; seeded from domain checkpoints.
public sealed class ClientDirectory : IClientDirectory
{
    private readonly ConcurrentDictionary<int, Client> _clients = new();

    public IReadOnlyCollection<Client> All => _clients.Values.ToList();

    public Client GetOrCreate(ClientId id)
    {
        return _clients.GetOrAdd(id.Id, key => new Client(new ClientId(key)));
    }

    public bool TryGet(ClientId id, out Client client) => _clients.TryGetValue(id.Id, out client!);

    public void ReadClients(int playerCount)
    {
        if (_clients.Count > 0)
        {
            return;
        }

        var cps = Checkpoints.Start;
        var count = Math.Min(playerCount, cps.Count);
        for (var i = 0; i < count; i++)
        {
            var cp = cps[i];
            var id = new ClientId(cp.Id);
            var client = GetOrCreate(id);
            client.SetCoordinates(cp.Location);
        }
    }
}
