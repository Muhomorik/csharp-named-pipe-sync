using System.Diagnostics;

namespace NamedPipeSync.Common.Domain;

[DebuggerDisplay("{Id}: ({Location.X}, {Location.Y})")]
public readonly record struct Checkpoint(int Id, Coordinate Location);
