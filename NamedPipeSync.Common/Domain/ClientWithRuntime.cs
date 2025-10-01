using System;
using System.Diagnostics;
using NamedPipeSync.Common.Domain;
using NamedPipeSync.Common.Infrastructure.Protocol;
using NamedPipeSync.Common.Application;

namespace NamedPipeSync.Common.Domain;

/// <summary>
///     Represents a client entity enriched with runtime state used by the application while the process is running.
/// </summary>
/// <remarks>
///     This type complements the simpler <see cref="Client"/> domain entity by carrying additional, ephemeral
///     runtime information such as the last checkpoint reached and the next checkpoint being targeted.
///     It does not perform I/O, UI operations, or process lifetime control; it is a pure domain model.
/// </remarks>
[DebuggerDisplay("Id = {Id}, Connection = {Connection}, Coordinates = {Coordinates}, Last = {LastCheckpoint.Id}, Next = {MovingToCheckpoint.Id}, OnCp = {IsOnCheckpoint}")]
public sealed class ClientWithRuntime
{
    private bool _isOnCheckpoint;

    /// <summary>
    ///     Creates a new instance with the specified client identifier and default runtime state.
    /// </summary>
    /// <param name="id">Unique client identifier. Must be a valid <see cref="ClientId"/> produced by the infrastructure layer.</param>
    [Obsolete("Provide a starting checkpoint to satisfy the non-null checkpoint invariant.")]
    public ClientWithRuntime(ClientId id)
        : this(id, Checkpoints.Start[0])
    {
    }

    /// <summary>
    ///     Creates a new instance with the specified client identifier and a required starting checkpoint.
    /// </summary>
    /// <param name="id">Unique client identifier. Must be a valid <see cref="ClientId"/> produced by the infrastructure layer.</param>
    /// <param name="startingCheckpoint">The initial checkpoint associated with this client; cannot be null.</param>
    public ClientWithRuntime(ClientId id, Checkpoint startingCheckpoint)
    {
        Id = id;
        Connection = ConnectionState.Disconnected;
        Coordinates = new Coordinate(0, 0);
        LastCheckpoint = startingCheckpoint;
        MovingToCheckpoint = startingCheckpoint;
        _isOnCheckpoint = false;
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
    ///     When <see cref="IsOnCheckpoint"/> is true, this value is automatically set to <see cref="LastCheckpoint"/>.
    /// </summary>
    public Checkpoint MovingToCheckpoint { get; set; }

    /// <summary>
    ///     Indicates whether the client is currently located on a checkpoint (within domain-defined tolerance).
    ///     Invariant: when set to true, the client is on the <see cref="LastCheckpoint"/>, which is also treated as the current checkpoint.
    ///     Side effect: setting to true aligns <see cref="MovingToCheckpoint"/> with <see cref="LastCheckpoint"/>.
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
    ///     Gets the current checkpoint context: when on a checkpoint, this is <see cref="LastCheckpoint"/>;
    ///     otherwise, returns <see cref="MovingToCheckpoint"/>.
    /// </summary>
    public Checkpoint CurrentCheckpoint => IsOnCheckpoint ? LastCheckpoint : MovingToCheckpoint;
}
