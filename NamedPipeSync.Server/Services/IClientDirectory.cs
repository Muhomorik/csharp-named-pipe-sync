using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Server.Services;

public interface IClientDirectory
{
    IReadOnlyCollection<Client> All { get; }
    IObservable<CoordinatesChanged> CoordinatesChanged { get; }
    Client GetOrCreate(ClientId id);
    bool TryGet(ClientId id, out Client client);
    void ReadClients();
}