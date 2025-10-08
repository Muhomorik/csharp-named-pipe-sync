using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Forms;
using NLog;

namespace NamedPipeSync.Client.UI;

/// <summary>
/// WPF implementation of <see cref="IScreenCaptureService"/> that captures the monitor where MainWindow resides.
/// </summary>
public sealed class ScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger _logger;

    public ScreenCaptureService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> CaptureCurrentScreenPngAsync(CancellationToken cancellationToken = default)
    {
        var window = System.Windows.Application.Current?.MainWindow;
        if (window is null)
            return Array.Empty<byte>();

        IntPtr hwnd = IntPtr.Zero;
        await window.Dispatcher.InvokeAsync(() => { hwnd = new WindowInteropHelper(window).Handle; });

        var screen = Screen.FromHandle(hwnd);
        var bounds = screen.Bounds;

        using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        await using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
