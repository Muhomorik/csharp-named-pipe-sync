using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using NLog;

namespace NamedPipeSync.Server.Services;

/// <summary>
/// WPF implementation of <see cref="IWindowStateService"/> that operates on <see cref="Application.Current"/>.MainWindow.
/// Ensures UI-thread marshalling via Dispatcher and avoids blocking the UI thread.
/// </summary>
public sealed class WindowStateService : IWindowStateService
{
    private readonly ILogger _logger;

    public WindowStateService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task MinimizeAsync(CancellationToken cancellationToken = default)
    {
        var window = Application.Current?.MainWindow;
        if (window is null)
            return;

        await window.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                window.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }, System.Windows.Threading.DispatcherPriority.Normal, cancellationToken);
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        var window = Application.Current?.MainWindow;
        if (window is null)
            return;

        await window.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;

                // Bring to front if possible
                if (!window.IsVisible)
                    window.Show();

                window.Activate();
                window.Topmost = true;  // toggle to ensure z-order
                window.Topmost = false;
                window.Focus();
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }, System.Windows.Threading.DispatcherPriority.Normal, cancellationToken);
    }
}
