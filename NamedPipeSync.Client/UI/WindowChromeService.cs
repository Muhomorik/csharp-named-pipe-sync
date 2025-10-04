using System;
using System.Windows;

namespace NamedPipeSync.Client.UI
{
    /// <summary>
    /// Simple implementation of IWindowChromeService.
    /// Computes chrome as the difference between outer window size and content size.
    /// Keeps last measurement and supports Reset for testability / restability.
    /// </summary>
    public class WindowChromeService : IWindowChromeService
    {
        private Size _lastChrome = Size.Empty;

        /// <inheritdoc />
        public Size ComputeChrome(double outerWidth, double outerHeight, double contentWidth, double contentHeight)
        {
            // Defensive: clamp NaN and negative inputs to zero where appropriate
            var ow = double.IsNaN(outerWidth) ? 0.0 : outerWidth;
            var oh = double.IsNaN(outerHeight) ? 0.0 : outerHeight;
            var cw = double.IsNaN(contentWidth) ? 0.0 : contentWidth;
            var ch = double.IsNaN(contentHeight) ? 0.0 : contentHeight;

            var chromeW = ow - cw;
            var chromeH = oh - ch;

            // Ensure chrome never becomes negative (shouldn't be in normal WPF layout, but guard anyway)
            if (chromeW < 0) chromeW = 0;
            if (chromeH < 0) chromeH = 0;

            _lastChrome = new Size(chromeW, chromeH);
            return _lastChrome;
        }

        /// <inheritdoc />
        public Size GetLastChrome() => _lastChrome;

        /// <inheritdoc />
        public void Reset() => _lastChrome = Size.Empty;
    }
}
