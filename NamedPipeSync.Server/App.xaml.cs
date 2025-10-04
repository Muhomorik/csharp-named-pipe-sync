using System;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Windows;

using Autofac;

using NamedPipeSync.Server.Services;
using NamedPipeSync.Server.ViewModels;

using NLog;

using System.IO;

using Autofac.Core;
using Autofac.Core.Resolving.Pipeline;

using NamedPipeSync.Common.Application;
using NamedPipeSync.Common.Infrastructure;

namespace NamedPipeSync.Server;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ILifetimeScope? _applicationScope;
    private IContainer? _container;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure NLog from config file
        LogManager.Setup().LoadConfigurationFromFile("NLog.config");

        // Configure Autofac container
        var builder = new ContainerBuilder();
        ConfigureServices(builder);
        _container = builder.Build();

        // Create application-wide lifetime scope
        _applicationScope = _container.BeginLifetimeScope();


        // Create and show the main window
        var mainWindow = _applicationScope.Resolve<MainWindow>();
        var mainWindowViewModel = _applicationScope.Resolve<MainWindowServerViewModel>();

        mainWindow.DataContext = mainWindowViewModel;
        mainWindow.Show();
    }

    private void ConfigureServices(ContainerBuilder builder)
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

        // Register all ViewModels automatically (logger resolves automatically).
        // Ensure every ViewModel gets DispatcherScheduler.Current injected for constructor parameters of type IScheduler
        // so they can marshal to the UI thread. Tests can still override IScheduler by registering different
        // components or using explicit registrations that take precedence.
        builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
            .Where(t => t.Name.EndsWith("ViewModel"))
            .AsSelf()
            .WithParameter(
                new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(IScheduler),
                    (pi, ctx) => DispatcherScheduler.Current))
            .InstancePerDependency();

        // Determine client executable path once and whether it exists on disk.
        var clientExecutablePath = GetClientExecutablePath();
        var clientExecutableMissing = !File.Exists(clientExecutablePath);

        // Override registration for MainWindowServerViewModel so we can supply the UI scheduler explicitly
        // and inform it whether the client executable is present on disk.
        // This ensures the viewmodel receives DispatcherScheduler.Current for UI marshalling while the
        // global IScheduler registration can remain TaskPoolScheduler.Default for background work.
        builder.RegisterType<MainWindowServerViewModel>()
            .AsSelf()
            .WithParameter(new TypedParameter(typeof(IScheduler), DispatcherScheduler.Current))
            .WithParameter(new TypedParameter(typeof(bool), clientExecutableMissing))
            .InstancePerDependency();

        // Register Views
        builder.RegisterType<MainWindow>();

        // Register server implementation and related services
        builder.RegisterType<NamedPipeServer>()
            .As<INamedPipeServer>()
            .SingleInstance();

        // Register runtime repository for clients
        builder.RegisterType<InMemoryClientWithRuntimeRepository>()
            .As<IClientWithRuntimeRepository>()
            .SingleInstance();

        builder.RegisterType<ClientWithRuntimeEventDispatcher>()
            .As<IClientWithRuntimeEventDispatcher>()
            .SingleInstance();

        builder.RegisterType<SimpleRingCoordinatesCalculator>()
            .As<ICoordinatesCalculator>()
            .SingleInstance();

        // Register the coordinates send scheduler with sensible defaults
        // Use TaskPoolScheduler.Default (registered below) for background timing
        builder.RegisterType<CoordinatesSendScheduler>()
            .As<ICoordinatesSendScheduler>()
            .WithParameter(new TypedParameter(typeof(TimeSpan), TimeSpan.FromMilliseconds(50)))
            .WithParameter(new TypedParameter(typeof(int), 4))
            .SingleInstance();

        // IScheduler for Rx: production uses TaskPoolScheduler; tests can override
        builder.RegisterInstance(TaskPoolScheduler.Default)
            .As<IScheduler>();

        // Client launcher (supply the client exe path)
        builder.Register(ctx => new ClientProcessLauncher(
            clientExecutablePath
        )).As<IClientProcessLauncher>().SingleInstance();

        // Fallback: Register all types from executing assembly.
        // The last registration for a service wins by default
        builder
            .RegisterAssemblyTypes(Assembly.GetExecutingAssembly()).AsSelf().AsImplementedInterfaces()
            .PreserveExistingDefaults();
        // - Keep the scan as-is but add .PreserveExistingDefaults() so it doesn’t override your earlier lambda registration.
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _applicationScope?.Dispose();
        _container?.Dispose();
        base.OnExit(e);
    }

    private static string GetClientExecutablePath()
    {
#if DEBUG
        // Use the project folder path during debugging
        var clientExecutablePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "NamedPipeSync.Client",
            "bin",
            "Debug",
            "net9.0-windows10.0.26100.0",
            "NamedPipeSync.Client.exe"
        );
        return Path.GetFullPath(clientExecutablePath);

#else
        // Use the deployed path in production
        return Path.Combine(AppContext.BaseDirectory, "NamedPipeSync.Client.exe");
#endif
    }
}
