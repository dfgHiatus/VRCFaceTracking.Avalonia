using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Jeek.Avalonia.Localization;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using VRCFaceTracking.Activation;
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
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        LiveCharts.Configure(config =>
                config.AddDarkTheme()
        );

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
        Localizer.SetLocalizer(new JsonLocalizer());
        Localizer.Language = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        var locator = new ViewLocator();
        DataTemplates.Add(locator);

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
            logging.AddConsole();
            logging.AddProvider(new OutputLogProvider(Dispatcher.UIThread));
            logging.AddProvider(new LogFileProvider());
        });
        ConfigureViewModels(services);
        ConfigureViews(services);

        // Default Activation Handler
        services.AddTransient<ActivationHandler, DefaultActivationHandler>();
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
        services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();

        services.AddSingleton<IActivationService, ActivationService>();
        services.AddSingleton<IDispatcherService, DispatcherService>();

        // Core Services
        services.AddTransient<IIdentityService, IdentityService>();
        services.AddSingleton<ModuleInstaller>();
        services.AddSingleton<IModuleDataService, ModuleDataService>();
        services.AddTransient<IFileService, FileService>();
        services.AddSingleton<OscQueryService>();
        services.AddSingleton<MulticastDnsService>();
        services.AddSingleton<IMainService, MainStandalone>();
        services.AddTransient<AvatarConfigParser>();
        services.AddTransient<OscQueryConfigParser>();
        services.AddSingleton<UnifiedTracking>();
        services.AddSingleton<ILibManager, UnifiedLibManager>();
        services.AddSingleton<IOscTarget, OscTarget>();
        services.AddSingleton<HttpHandler>();
        services.AddSingleton<OscSendService>();
        services.AddSingleton<OscRecvService>();
        services.AddSingleton<ParameterSenderService>();
        services.AddSingleton<UnifiedTrackingMutator>();
        services.AddTransient<GithubService>();

        services.AddHostedService<ParameterSenderService>(provider => provider.GetService<ParameterSenderService>()!);
        services.AddHostedService<OscRecvService>(provider => provider.GetService<OscRecvService>()!);

        if (!File.Exists(LocalSettingsService.DefaultLocalSettingsFile))
        {
            // Create the file if it doesn't exist
            File.Create(LocalSettingsService.DefaultLocalSettingsFile).Dispose();
        }

        // Configuration
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile(LocalSettingsService.DefaultLocalSettingsFile)
            .Build();
        services.Configure<LocalSettingsOptions>(config);

        var provider = services.BuildServiceProvider();

        Ioc.Default.ConfigureServices(provider);

        var activation = Ioc.Default.GetRequiredService<IActivationService>();
        Task.Run(() => activation.ActivateAsync(null));

        var vrcft = Ioc.Default.GetRequiredService<IMainService>();
        Task.Run(() => vrcft.InitializeAsync());

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
        Task.Run(() => vrcft.Teardown());
    }

    private void OnTrayShutdownClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [Singleton(typeof(MainViewModel))]
    [Transient(typeof(OutputPageViewModel))]
    [Transient(typeof(ModuleRegistryViewModel))]
    [Transient(typeof(SettingsPageViewModel))]
    [Transient(typeof(HomePageViewModel))]
    [SuppressMessage("CommunityToolkit.Extensions.DependencyInjection.SourceGenerators.InvalidServiceRegistrationAnalyzer", "TKEXDI0004:Duplicate service type registration")]
    internal static partial void ConfigureViewModels(IServiceCollection services);

    [Singleton(typeof(MainWindow))]
    [Transient(typeof(OutputPageView))]
    [Transient(typeof(ModuleRegistryView))]
    [Transient(typeof(SettingsPageView))]
    [Transient(typeof(HomePageView))]
    internal static partial void ConfigureViews(IServiceCollection services);
}
