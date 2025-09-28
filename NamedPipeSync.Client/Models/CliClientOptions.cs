using System.Diagnostics;
using CommandLine;

namespace NamedPipeSync.Client.Models;

/// <summary>
///     CLI options of the client.
/// </summary>
[DebuggerDisplay("CliClientOptions: Id = {Id}")]
public class CliClientOptions
{
    // Supports: --id 123, -i 123, --id=123; defaults to -1 when not provided
    [Option('i', "id", Required = false, Default = -1, HelpText = "Client ID.")]
    public int Id { get; init; }
}