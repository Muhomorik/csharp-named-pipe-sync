using System.Diagnostics;
using System.Reactive.Subjects;

using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Domain;

[DebuggerDisplay("Id = {Id}, Connection = {Connection}, Coordinates = {Coordinates}")]
public sealed class Client
{
    // TODO: this is cheesy, I don't like it.
    private readonly Subject<CoordinatesChanged> _coordinatesChanged = new();

    public Client(ClientId id)
    {
        Id = id;
        Connection = ConnectionState.Disconnected;
        Coordinates = new Coordinate(0, 0);
    }

    public ClientId Id { get; }
    public ConnectionState Connection { get; private set; }
    public Coordinate Coordinates { get; private set; }

    public IObservable<CoordinatesChanged> CoordinatesChanged => _coordinatesChanged;

    public void SetConnection(ConnectionState state)
    {
        if (Connection != state)
        {
            Connection = state;
        }
    }

    public void SetCoordinates(Coordinate coords)
    {
        if (!coords.Equals(Coordinates))
        {
            Coordinates = coords;
            _coordinatesChanged.OnNext(new CoordinatesChanged(Id, coords));
        }
    }
}