using System.Configuration;
using System.Data;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Windows;

using Autofac;
using Autofac.Core;
using Autofac.Core.Resolving.Pipeline;

using CommandLine;
using CommandLine.Text;

using NamedPipeSync.Client.Models;
using NamedPipeSync.Client.ViewModels;
using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Infrastructure;
using NamedPipeSync.Common.Infrastructure.Protocol;
using NamedPipeSync.Client.UI;
using NamedPipeSync.Common.Application.Imaging;

using NLog;

namespace NamedPipeSync.Client;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ILifetimeScope? _applicationScope;
    private IContainer? _container;

    /// <summary>
    ///     Application startup: parse CLI (infrastructure), compose dependencies, and show the main window.
    /// </summary>
    /// <param name="e">Startup event arguments containing the raw CLI args.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure NLog from config file
        LogManager.Setup().LoadConfigurationFromFile("NLog.config");

        // Parse CLI options (infrastructure concern)
        var cliOptions = ParseOptionsOrExit(e.Args);

        // Configure Autofac container, passing parsed options to register an application-level context abstraction
        var builder = new ContainerBuilder();
        ConfigureServices(builder, cliOptions);
        _container = builder.Build();

        // Create application-wide lifetime scope
        _applicationScope = _container.BeginLifetimeScope();

        // Create and show the main window
        var mainWindow = _applicationScope.Resolve<MainWindow>();
        var mainWindowViewModel = _applicationScope.Resolve<MainWindowClientViewModel>();

        mainWindow.DataContext = mainWindowViewModel;
        mainWindow.Show();
    }

    /// <summary>
    ///     Registers application services with Autofac.
    /// </summary>
    /// <param name="builder">The Autofac container builder.</param>
    /// <param name="cliOptions">
    ///     Parsed CLI options. Used only here to adapt infrastructure into an application-level abstraction (
    ///     <see cref="IClientContext" />).
    /// </param>
    private void ConfigureServices(ContainerBuilder builder, CliClientOptions cliOptions)
    {
        // Global logger injection: automatically provide NLog.ILogger with the consuming component's type
        builder.RegisterCallback(cr =>
        {
            cr.Registered += (sender, args) =>
            {
                args.ComponentRegistration.PipelineBuilding += (s, builder) =>
                {
                    builder.Use(PipelinePhase.ParameterSelection, (context, next) =>
                    {
                        var implType = args.ComponentRegistration.Activator.LimitType;
                        var newParams = context.Parameters.Union(new[]
                        {
                            new ResolvedParameter(
                                (pi, ctx) => pi.ParameterType == typeof(ILogger),
                                (pi, ctx) => LogManager.GetLogger(implType.FullName))
                        });
                        context.ChangeParameters(newParams);
                        next(context);
                    });
                };
            };
        });

        // Expose only an application-level abstraction to the rest of the app (DDD: depend on abstractions).
        // SingleInstance is appropriate because CLI-derived context is constant for the app lifetime.
        builder.RegisterInstance(new ClientContext(cliOptions))
            .As<IClientContext>()
            .SingleInstance();

        // Register all ViewModels automatically (logger resolves automatically)
        builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
            .Where(t => t.Name.EndsWith("ViewModel"))
            .AsSelf()
            .InstancePerDependency();

        // Register UI scheduler (marshal Rx to GUI thread); testable via IScheduler
        builder.RegisterInstance(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .As<IScheduler>()
            .SingleInstance();

        // Register INamedPipeClient with implementation and per-app lifetime
        builder.Register(ctx =>
            {
                var clientContext = ctx.Resolve<IClientContext>();
                return new NamedPipeClient(new ClientId(clientContext.ClientId));
            })
            .As<INamedPipeClient>()
            .SingleInstance();

        // Register Views
        builder.RegisterType<MainWindow>();

        // UI services
        builder.RegisterType<WindowStateService>()
            .As<IWindowStateService>()
            .SingleInstance();
        builder.RegisterType<ScreenCaptureService>()
            .As<IScreenCaptureService>()
            .SingleInstance();

        // Imaging
        builder.RegisterType<ImageBase64Converter>()
            .As<IImageBase64Converter>()
            .SingleInstance();

        // Application lifetime service (allows VMs to request shutdown without WPF dependency)
        builder.RegisterType<ApplicationLifetime>()
            .As<IApplicationLifetime>()
            .SingleInstance();

        // Register MainWindow model
        builder.RegisterType<MainWindowModel>()
            .As<IMainWindowModel>()
            .InstancePerDependency();

        // Fallback: Register all types from executing assembly
        //builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly()).AsSelf().AsImplementedInterfaces();
    }

    /// <summary>
    ///     Parses CLI options. On invalid input, shows help and terminates the process with exit code 1.
    ///     This keeps CLI parsing as an infrastructure concern confined to the composition root.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <returns>Parsed <see cref="CliClientOptions" />.</returns>
    private static CliClientOptions ParseOptionsOrExit(string[] args)
    {
        var result = Parser.Default.ParseArguments<CliClientOptions>(args);

        CliClientOptions? options = null;

        result
            .WithParsed(o => options = o)
            .WithNotParsed(errs =>
            {
                var helpText = HelpText.AutoBuild(result, h =>
                {
                    h.AdditionalNewLineAfterOption = true;
                    h.Heading = "NamedPipeSync";
                    h.Copyright = "";
                    return h;
                }, e => e);

                MessageBox.Show(
                    helpText.ToString(),
                    "Invalid command line",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Environment.Exit(1);
            });

        return options!;
    }

    /// <summary>
    ///     Disposes the application-wide scope and container on exit.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _applicationScope?.Dispose();
            _container?.Dispose();
        }
        finally
        {
            base.OnExit(e);
        }
    }
}
