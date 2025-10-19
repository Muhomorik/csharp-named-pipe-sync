using System.Windows;

namespace NamedPipeSync.Server.Services;

/// <summary>
/// Implementation of <see cref="IWindowProvider"/> that provides access to the application's main window.
/// The window reference is set by the application during startup.
/// </summary>
public sealed class WindowProvider : IWindowProvider
{
    /// <summary>
    /// Gets or sets the current main window.
    /// This is typically set by the application during window initialization.
    /// </summary>
    public Window? MainWindow { get; set; }
}
