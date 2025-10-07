using System.Diagnostics;

using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Infrastructure.Protocol;

namespace NamedPipeSync.Common.Domain;

/// <summary>
///     Represents a client entity enriched with runtime state used by the application while the process is running.
/// </summary>
/// <remarks>
///     This type complements the simpler <see cref="Client" /> domain entity by carrying additional, ephemeral
///     runtime information such as the last checkpoint reached and the next checkpoint being targeted.
///     It does not perform I/O, UI operations, or process lifetime control; it is a pure domain model.
/// </remarks>
[DebuggerDisplay(
    "Id = {Id}, Connection = {Connection}, Coordinates = {Coordinates}, Last = {LastCheckpoint.Id}, Next = {MovingToCheckpoint.Id}, OnCp = {IsOnCheckpoint}")]
public sealed class ClientWithRuntime
{
    private bool _isOnCheckpoint;


    /// <summary>
    ///     Creates a new instance with the specified client identifier and a required starting checkpoint.
    /// </summary>
    /// <param name="id">
    /// Unique client identifier. Must be a valid <see cref="ClientId" /> produced by the infrastructure
    ///     layer.
    /// </param>
    /// <param name="startingCheckpoint">The initial checkpoint associated with this client; cannot be null.</param>
    /// <param name="renderCheckpoint">The checkpoint used for rendering context; cannot be changed after construction.</param>
    public ClientWithRuntime(ClientId id, Checkpoint startingCheckpoint, Checkpoint renderCheckpoint)
    {
        Id = id;
        Connection = ConnectionState.Disconnected;
        Coordinates = new Coordinate(0, 0);
        LastCheckpoint = startingCheckpoint;
        MovingToCheckpoint = startingCheckpoint;
        _isOnCheckpoint = false;
        RenderCheckpoint = renderCheckpoint;
    }

    /// <summary>
    ///     Unique client identifier.
    /// </summary>
    public ClientId Id { get; }

    /// <summary>
    ///     Current connection state of the client.
    /// </summary>
    public ConnectionState Connection { get; set; }

    /// <summary>
    ///     Current coordinates of the client in the domain space.
    /// </summary>
    public Coordinate Coordinates { get; set; }

    /// <summary>
    ///     The last checkpoint that the client has reached. Never null by design.
    /// </summary>
    public Checkpoint LastCheckpoint { get; set; }

    /// <summary>
    ///     The checkpoint the client is currently moving to. Never null; the client is always moving towards a checkpoint.
    ///     When <see cref="IsOnCheckpoint" /> is true, this value is automatically set to <see cref="LastCheckpoint" />.
    /// </summary>
    public Checkpoint MovingToCheckpoint { get; set; }

    /// <summary>
    ///     Indicates whether the client is currently located on a checkpoint (within domain-defined tolerance).
    ///     Invariant: when set to true, the client is on the <see cref="LastCheckpoint" />, which is also treated as the
    ///     current checkpoint.
    ///     Side effect: setting to true aligns <see cref="MovingToCheckpoint" /> with <see cref="LastCheckpoint" />.
    /// </summary>
    public bool IsOnCheckpoint
    {
        get => _isOnCheckpoint;
        set
        {
            _isOnCheckpoint = value;
            if (value)
            {
                // When on a checkpoint, the current/target checkpoint is the last one reached.
                if (!MovingToCheckpoint.Equals(LastCheckpoint))
                {
                    MovingToCheckpoint = LastCheckpoint;
                }
            }
        }
    }

    /// <summary>
    ///     Gets the current checkpoint context: when on a checkpoint, this is <see cref="LastCheckpoint" />;
    ///     otherwise, returns <see cref="MovingToCheckpoint" />.
    /// </summary>
    public Checkpoint CurrentCheckpoint => IsOnCheckpoint ? LastCheckpoint : MovingToCheckpoint;

    /// <summary>
    ///     The checkpoint used for rendering/contextual purposes. Immutable after construction.
    /// </summary>
    public Checkpoint RenderCheckpoint { get; }

    // Encapsulated state transition helpers to avoid external direct property sets

    /// <summary>
    ///     Initializes the position to the starting checkpoint if coordinates are unset (0,0).
    ///     Sets IsOnCheckpoint to true. Returns true if initialization occurred.
    /// </summary>
    public bool InitializePositionIfNeeded()
    {
        if (Coordinates.X == 0 && Coordinates.Y == 0)
        {
            Coordinates = LastCheckpoint.Location;
            IsOnCheckpoint = true;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Updates the runtime state to reflect that the client has arrived at the specified checkpoint.
    ///     Sets coordinates to the checkpoint location, updates LastCheckpoint and IsOnCheckpoint, and advances
    ///     MovingToCheckpoint to the next checkpoint in the ring.
    /// </summary>
    /// <param name="target">The checkpoint that was reached.</param>
    public void ArriveAt(Checkpoint target)
    {
        Coordinates = target.Location;
        LastCheckpoint = target;
        IsOnCheckpoint = true;
        MovingToCheckpoint = NextCheckpointAfter(target);
    }

    /// <summary>
    ///     Advances coordinates by up to the provided step toward the specified target checkpoint and clears IsOnCheckpoint.
    ///     Returns the new coordinates.
    /// </summary>
    public Coordinate MoveStepTowards(Checkpoint target, double step)
    {
        var current = Coordinates;
        var dx = target.Location.X - current.X;
        var dy = target.Location.Y - current.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var actual = Math.Min(step, distance);
        var nx = current.X + actual * (dx / (distance == 0 ? 1 : distance));
        var ny = current.Y + actual * (dy / (distance == 0 ? 1 : distance));
        var next = new Coordinate(nx, ny);
        Coordinates = next;
        IsOnCheckpoint = false;
        return next;
    }

    private static Checkpoint NextCheckpointAfter(Checkpoint current)
    {
        var cps = Checkpoints.Start;
        var idx = Math.Max(0, cps.ToList().FindIndex(cp => cp.Id == current.Id));
        var nextIdx = (idx + 1) % cps.Count;
        return cps[nextIdx];
    }
}
