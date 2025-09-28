using System.Collections.Concurrent;
using System.Reactive.Subjects;

using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Server.Services;

// TODO: this is repository, I don't like the idea of raising events here.
public sealed class ClientDirectory : IClientDirectory
{
    private readonly ConcurrentDictionary<int, Client> _clients = new();
    private readonly Subject<CoordinatesChanged> _coordsChanged = new();

    public IReadOnlyCollection<Client> All => _clients.Values.ToList();
    public IObservable<CoordinatesChanged> CoordinatesChanged => _coordsChanged;

    public Client GetOrCreate(ClientId id)
    {
        return _clients.GetOrAdd(id.Id, key =>
        {
            var c = new Client(new ClientId(key));
            c.CoordinatesChanged.Subscribe(_coordsChanged);
            return c;
        });
    }

    public bool TryGet(ClientId id, out Client client) => _clients.TryGetValue(id.Id, out client!);

    public void ReadClients()
    {
        if (_clients.Count > 0)
        {
            return;
        }

        // Coordinates (x, y top-left):
        // Row 0: (48,208), (368,208), (688,208)
        // Row 1: (48,528), (368,528), (688,528)
        // TODO: They will rotate every 10 seconds, id should start from the last column on the right.
        var coords = new (int X, int Y)[]
        {
            (48, 208), (368, 208), (688, 208),
            (48, 528), (368, 528), (688, 528)
        };

        for (var i = 0; i < coords.Length; i++)
        {
            var id = new ClientId(i + 1);
            var client = GetOrCreate(id);
            client.SetCoordinates(new Coordinate(coords[i].X, coords[i].Y));
        }
    }
}