using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NamedPipeSync.Client.UI;

namespace NamedPipeSync.Client;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow // : Window
{
    // chrome stored locally for quick access (delegated calculation to service)
    private double _chromeWidth = double.NaN;
    private double _chromeHeight = double.NaN;

    // avoid feedback loops while programmatically setting sizes
    private bool _suppressUpdates;

    private FrameworkElement? _contentElement;

    // The reusable service that computes chrome; can be replaced in tests or via DI
    private readonly IWindowChromeService _chromeService;

    public MainWindow()
        : this(new WindowChromeService()) // keep parameterless ctor for XAML; delegate to main ctor
    {
    }

    // Allow injection of a different IWindowChromeService (useful for tests or DI)
    public MainWindow(IWindowChromeService? chromeService)
    {
        _chromeService = chromeService ?? new WindowChromeService();
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        _contentElement = Content as FrameworkElement;
        if (_contentElement == null)
            return;

        // subscribe to VM changes
        if (DataContext is INotifyPropertyChanged inpc)
            inpc.PropertyChanged += ViewModel_PropertyChanged;

        // update content/window relationships
        _contentElement.SizeChanged += ContentElement_SizeChanged;
        SizeChanged += Window_SizeChanged;
        LayoutUpdated += MainWindow_LayoutUpdated;

        // initialize sizes based on VM (if present)
        if (DataContext is NamedPipeSync.Client.ViewModels.MainWindowClientViewModel vm)
        {
            // compute current chrome sizes now that layout is loaded using the service
            UpdateChromeSizes();

            _suppressUpdates = true;
            Width = vm.WindowContentWidth + _chromeWidth;
            Height = vm.WindowContentHeight + _chromeHeight;
            _suppressUpdates = false;
        }
    }

    private void MainWindow_LayoutUpdated(object? sender, EventArgs e)
    {
        // detect changes in chrome (for example when border/titlebar visibility changes)
        if (_contentElement == null)
            return;

        var newChrome = _chromeService.ComputeChrome(ActualWidth, ActualHeight, _contentElement.ActualWidth, _contentElement.ActualHeight);
        var newChromeW = newChrome.Width;
        var newChromeH = newChrome.Height;

        // small tolerance to avoid jitter
        if (double.IsNaN(_chromeWidth) ||
            Math.Abs(newChromeW - _chromeWidth) > 0.5 ||
            Math.Abs(newChromeH - _chromeHeight) > 0.5)
        {
            _chromeWidth = newChromeW;
            _chromeHeight = newChromeH;

            if (DataContext is NamedPipeSync.Client.ViewModels.MainWindowClientViewModel vm)
            {
                // re-apply VM desired content size to the outer window so the content remains equal
                _suppressUpdates = true;
                Width = vm.WindowContentWidth + _chromeWidth;
                Height = vm.WindowContentHeight + _chromeHeight;
                _suppressUpdates = false;
            }
        }
    }

    private void UpdateChromeSizes()
    {
        if (_contentElement == null)
            return;

        var chrome = _chromeService.ComputeChrome(ActualWidth, ActualHeight, _contentElement.ActualWidth, _contentElement.ActualHeight);
        _chromeWidth = chrome.Width;
        _chromeHeight = chrome.Height;
    }

    private void ContentElement_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_suppressUpdates)
            return;

        if (DataContext is NamedPipeSync.Client.ViewModels.MainWindowClientViewModel vm && _contentElement != null)
        {
            // keep VM in sync with actual content size (user-resize or layout changes)
            vm.WindowContentWidth = Math.Max(0, _contentElement.ActualWidth);
            vm.WindowContentHeight = Math.Max(0, _contentElement.ActualHeight);
        }
    }

    private void Window_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_suppressUpdates)
            return;

        // when window outer size changes (user drag), update VM to reflect the resulting content size
        if (DataContext is NamedPipeSync.Client.ViewModels.MainWindowClientViewModel vm && _contentElement != null)
        {
            vm.WindowContentWidth = Math.Max(0, _contentElement.ActualWidth);
            vm.WindowContentHeight = Math.Max(0, _contentElement.ActualHeight);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Respond to VM changes: apply VM content size to outer window (accounting for chrome)
        if (_suppressUpdates)
            return;

        if (sender is NamedPipeSync.Client.ViewModels.MainWindowClientViewModel vm)
        {
            if (e.PropertyName == nameof(vm.WindowContentWidth) || e.PropertyName == nameof(vm.WindowContentHeight))
            {
                // Ensure we run on UI thread and after layout so chrome size is correct
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    UpdateChromeSizes();

                    _suppressUpdates = true;
                    Width = vm.WindowContentWidth + _chromeWidth;
                    Height = vm.WindowContentHeight + _chromeHeight;
                    _suppressUpdates = false;
                }), DispatcherPriority.Background);
            }
        }
    }
}