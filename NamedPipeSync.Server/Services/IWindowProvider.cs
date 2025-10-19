using System.Windows;

namespace NamedPipeSync.Server.Services;

/// <summary>
/// Provides access to the application's main window.
/// This abstraction allows services to access the window without breaking MVVM separation.
/// </summary>
public interface IWindowProvider
{
    /// <summary>
    /// Gets the current main window. May be null if called before the window is initialized.
    /// </summary>
    Window? MainWindow { get; }
}
