using System.Diagnostics;

using DevExpress.Mvvm;

using NamedPipeSync.Common.Application;

namespace NamedPipeSync.Server.ViewModels;

[DebuggerDisplay("Id = {Id}, Connection = {Connection}, Position = ({X}, {Y})")]
public sealed class ClientWithStateBindingItem : ViewModelBase
{
    public int Id
    {
        get => id;
        set => SetProperty(ref id, value, nameof(Id));
    }

    public ConnectionState Connection
    {
        get => connection;
        set => SetProperty(ref connection, value, nameof(Connection));
    }

    public double X
    {
        get => x;
        set => SetProperty(ref x, value, nameof(X));
    }

    public double Y
    {
        get => y;
        set => SetProperty(ref y, value, nameof(Y));
    }

    // Backing fields for properties
    private int id;
    private ConnectionState connection;
    private double x;
    private double y;
}