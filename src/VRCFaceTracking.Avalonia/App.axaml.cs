using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Activation;
using VRCFaceTracking.Avalonia.Controls;
using VRCFaceTracking.Avalonia.Services;
using VRCFaceTracking.Avalonia.ViewModels;
using VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;
using VRCFaceTracking.Avalonia.Views;
using VRCFaceTracking.Contracts.Services;
using VRCFaceTracking.Core;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.mDNS;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.OSC.Query.mDNS;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Services;
using VRCFaceTracking.Models;
using VRCFaceTracking.Services;

namespace VRCFaceTracking.Avalonia;

public partial class App : Application
{
    public static event Action<NotificationModel> SendNotification;

    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // https://github.com/benaclejames/VRCFaceTracking/blob/51405d57cbbd46c92ff176d5211d043ed875ad42/VRCFaceTracking/App.xaml.cs#L61C9-L71C10
        // Check for a "reset" file in the root of the app directory.
        // If one is found, wipe all files from inside it and delete the file.
        var resetFile = Path.Combine(VRCFaceTracking.Core.Utils.PersistentDataDirectory, "reset");
        if (File.Exists(resetFile))
        {
            // Delete everything including files and folders in Utils.PersistentDataDirectory
            foreach (var file in Directory.EnumerateFiles(VRCFaceTracking.Core.Utils.PersistentDataDirectory, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var locator = new ViewLocator();
        DataTemplates.Add(locator);

        var hostBuilder = Host.CreateDefaultBuilder().
            ConfigureServices((context, services) =>
            {
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddDebug();
                    logging.AddConsole();
                    logging.AddProvider(new OutputLogProvider(Dispatcher.UIThread));
                    logging.AddProvider(new LogFileProvider());
                });

                // Default Activation Handler
                services.AddTransient<ActivationHandler, DefaultActivationHandler>();
                services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

                services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
                services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
                services.AddSingleton<ILanguageSelectorService, LanguageSelectorService>();

                services.AddSingleton<IActivationService, ActivationService>();
                services.AddSingleton<IDispatcherService, DispatcherService>();

                // Core Services
                services.AddTransient<IIdentityService, IdentityService>();
                services.AddSingleton<ModuleInstaller>();
                services.AddSingleton<IModuleDataService, ModuleDataService>();
                services.AddTransient<IFileService, FileService>();
                services.AddSingleton<OscQueryService>();
                services.AddSingleton<MulticastDnsService>();
                services.AddTransient<AvatarConfigParser>();
                services.AddTransient<OscQueryConfigParser>();
                services.AddSingleton<ILibManager, UnifiedLibManager>();
                services.AddSingleton<IOscTarget, OscTarget>();
                services.AddSingleton<HttpHandler>();
                services.AddSingleton<OscRecvService>();
                services.AddSingleton<OscSendService>();
                services.AddSingleton<ParameterSenderService>();
                services.AddSingleton<UnifiedTrackingMutator>();
                services.AddSingleton<UnifiedTracking>();
                services.AddTransient<GithubService>();
                services.AddSingleton<IMainService, MainStandalone>();

                services.AddSingleton<DropOverlayService>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();

                services.AddTransient<OutputPageViewModel>();
                services.AddTransient<ModuleRegistryViewModel>();
                services.AddTransient<SettingsPageViewModel>();
                services.AddTransient<HomePageViewModel>();
                services.AddTransient<MutatorPageViewModel>();
                services.AddTransient<OutputPageView>();
                services.AddTransient<ModuleRegistryView>();
                services.AddTransient<SettingsPageView>();
                services.AddTransient<HomePageView>();
                services.AddTransient<MutatorPageView>();

                services.AddHostedService(provider => provider.GetService<OscRecvService>()!);
                services.AddHostedService(provider => provider.GetService<ParameterSenderService>()!);

                // Configuration
                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile(LocalSettingsService.DefaultLocalSettingsFile)
                    .Build();
                services.Configure<LocalSettingsOptions>(config);
            });

        if (!File.Exists(LocalSettingsService.DefaultLocalSettingsFile))
        {
            // Create the file if it doesn't exist
            File.Create(LocalSettingsService.DefaultLocalSettingsFile).Dispose();
        }

        _host = hostBuilder.Build();
        Ioc.Default.ConfigureServices(_host.Services);

        Task.Run(async () => await _host.StartAsync());

        var activation = Ioc.Default.GetRequiredService<IActivationService>();
        Task.Run(async () => await activation.ActivateAsync(null));

        var vm = Ioc.Default.GetRequiredService<MainViewModel>();
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow(vm);
                desktop.ShutdownRequested += OnShutdown;
                break;
            case ISingleViewApplicationLifetime singleViewPlatform:
                singleViewPlatform.MainView = new MainView { DataContext = vm };
                break;
        }

        // var notif = new NotificationModel();
        // notif.Title = "Hello, World!";
        // notif.Body = "This is a test notification.";
        // SendNotification?.Invoke(notif);

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdown(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

        var vrcft = Ioc.Default.GetRequiredService<IMainService>();
        Task.Run(vrcft.Teardown);
    }

    private void OnTrayShutdownClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
