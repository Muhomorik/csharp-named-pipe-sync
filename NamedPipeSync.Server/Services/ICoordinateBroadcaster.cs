namespace NamedPipeSync.Server.Services;

public interface ICoordinateBroadcaster : IDisposable
{
    IDisposable Start();
}