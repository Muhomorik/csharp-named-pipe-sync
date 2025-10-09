using System.Reactive.Linq;

using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Domain;


namespace NamedPipeSync.Client.ViewModels;

internal sealed class DesignTimePipeClient : INamedPipeClient
{
    public IObservable<ClientConnectionStateChange> ConnectionChanged { get; } =
        Observable.Empty<ClientConnectionStateChange>();

    public IObservable<Coordinate> Coordinates { get; } = Observable.Empty<Coordinate>();

    public IObservable<NamedPipeSync.Common.Infrastructure.Protocol.ServerSendsConfigurationMessage> ConfigurationReceived { get; } = Observable.Empty<NamedPipeSync.Common.Infrastructure.Protocol.ServerSendsConfigurationMessage>();

    public Task ConnectAsync(TimeSpan? retryDelay = null, CancellationToken ct = default) => Task.CompletedTask;

    public Task SendByeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task DisconnectAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
    }
}
