using System.Collections.Generic;

namespace NamedPipeSync.Common.Domain;

public static class Checkpoints
{
    // Start coordinates are: (48,208), (368,208), (688,208), (688,528), (368,528), (48,528)
    public static IReadOnlyList<Checkpoint> Start { get; } = new List<Checkpoint>
    {
        new(1, new Coordinate(48, 208)),
        new(2, new Coordinate(368, 208)),
        new(3, new Coordinate(688, 208)),
        new(4, new Coordinate(688, 528)),
        new(5, new Coordinate(368, 528)),
        new(6, new Coordinate(48, 528)),
    };
}
