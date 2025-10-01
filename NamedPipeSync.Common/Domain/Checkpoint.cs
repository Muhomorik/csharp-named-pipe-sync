using System.Diagnostics;

namespace NamedPipeSync.Common.Domain;

[DebuggerDisplay("{Id}: ({Location.X}, {Location.Y})")]
public readonly record struct Checkpoint
{
    public Checkpoint(int id, Coordinate location)
    {
        Id = id;
        Location = location;
    }

    public int Id { get; }
    public Coordinate Location { get; }
}
