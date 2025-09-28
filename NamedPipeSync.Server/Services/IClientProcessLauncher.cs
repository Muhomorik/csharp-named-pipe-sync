using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Server.Services;

public interface IClientProcessLauncher
{
    void StartClient(ClientId id);
    Task StartManyAsync(IEnumerable<ClientId> ids, CancellationToken ct = default);
}