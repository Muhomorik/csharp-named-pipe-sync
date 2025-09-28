using System.Diagnostics;

using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Server.Services;

public sealed class ClientProcessLauncher : IClientProcessLauncher
{
    private readonly string _clientExePath;

    public ClientProcessLauncher(string clientExePath) => _clientExePath = clientExePath;

    public void StartClient(ClientId id)
    {
        if (id.Id < 1 || id.Id > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "ClientId must be between 1 and 6 inclusive.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = _clientExePath,
            UseShellExecute = false,
            CreateNoWindow = false,
            ArgumentList = { "--id", id.Id.ToString() }
        };
        Process.Start(psi);
    }

    public async Task StartManyAsync(IEnumerable<ClientId> ids, CancellationToken ct = default)
    {
        var list = ids.Take(6).ToList();
        foreach (var id in list)
        {
            ct.ThrowIfCancellationRequested();
            StartClient(id);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}