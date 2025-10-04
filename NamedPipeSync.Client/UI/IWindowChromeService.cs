using System.Windows;

namespace NamedPipeSync.Client.UI
{
    /// <summary>
    /// Service that computes and stores the window chrome size (outer size minus content size).
    /// Designed to be pure and testable: given outer/content sizes it returns chrome and stores the last value.
    /// It also supports resetting stored state.
    /// </summary>
    public interface IWindowChromeService
    {
        /// <summary>
        /// Compute chrome as (outer - content) and store the result as the last measured chrome.
        /// </summary>
        /// <param name="outerWidth">Window ActualWidth</param>
        /// <param name="outerHeight">Window ActualHeight</param>
        /// <param name="contentWidth">Content ActualWidth</param>
        /// <param name="contentHeight">Content ActualHeight</param>
        /// <returns>Size where Width = chrome width, Height = chrome height</returns>
        Size ComputeChrome(double outerWidth, double outerHeight, double contentWidth, double contentHeight);

        /// <summary>
        /// Get the last measured chrome. If none measured, returns Size.Empty (Width/Height = 0).
        /// </summary>
        Size GetLastChrome();

        /// <summary>
        /// Reset stored chrome state (makes GetLastChrome return Size.Empty).
        /// </summary>
        void Reset();
    }
}
