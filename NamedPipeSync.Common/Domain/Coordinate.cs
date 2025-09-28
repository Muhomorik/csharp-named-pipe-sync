namespace NamedPipeSync.Common.Domain;

/// <summary>
///     A lightweight value object representing a 2D coordinate in the domain.
/// </summary>
/// <remarks>
///     Implemented as a readonly record struct for efficient value semantics and immutability.
/// </remarks>
public readonly record struct Coordinate(double X, double Y)
{
    public override string ToString() => $"({X}, {Y})";

    public static Coordinate FromTuple((double X, double Y) t) => new(t.X, t.Y);

    public (double X, double Y) ToTuple() => (X, Y);
}