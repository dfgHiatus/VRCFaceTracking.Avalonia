using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

using VRCFaceTracking.Avalonia;
using VRCFaceTracking.Contracts.Services;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Services;

public class ActivationService(
    IThemeSelectorService themeSelectorService,
    ILanguageSelectorService languageSelectorService,
    OscQueryService parameterOutputService,
    IMainService mainService,
    IModuleDataService moduleDataService,
    ModuleInstaller moduleInstaller,
    ILibManager libManager,
    ILogger<ActivationService> logger)
    : IActivationService
{
    public async Task ActivateAsync(object activationArgs)
    {
        // Execute tasks before activation.
        await InitializeAsync();

        // Execute tasks after activation.
        await StartupAsync();

        await themeSelectorService.SetRequestedThemeAsync();
        await languageSelectorService.SetRequestedLanguageAsync();
    }

    private async Task InitializeAsync()
    {
        await themeSelectorService.InitializeAsync().ConfigureAwait(false);
        await languageSelectorService.InitializeAsync().ConfigureAwait(false);

        await Task.CompletedTask;
    }

    private async Task StartupAsync()
    {
        logger.LogInformation("VRCFT Version {version} initializing...", Assembly.GetExecutingAssembly().GetName().Version);

        logger.LogInformation("Initializing OSC...");
        await parameterOutputService.InitializeAsync().ConfigureAwait(false);

        logger.LogInformation("Initializing main service...");
        await mainService.InitializeAsync().ConfigureAwait(false);

        // Before we initialize, we need to delete pending restart modules and check for updates for all our installed modules
        logger.LogDebug("Checking for deletion requests for installed modules...");
        var needsDeleting = moduleDataService.GetInstalledModules()
            .Where(m => m.InstallationState == InstallState.AwaitingRestart);
        foreach (var deleteModule in needsDeleting)
        {
            moduleInstaller.UninstallModule(deleteModule);
        }

        logger.LogInformation("Checking for updates for installed modules...");
        var localModules = moduleDataService.GetInstalledModules().Where(m => m.ModuleId != Guid.Empty);
        var remoteModules = await moduleDataService.GetRemoteModules();
        var outdatedModules = remoteModules.Where(rm => localModules.Any(lm =>
        {
            if (rm.ModuleId != lm.ModuleId)
                return false;

            var remoteVersion = new Version(rm.Version);
            var localVersion = new Version(lm.Version);

            return remoteVersion.CompareTo(localVersion) > 0;
        }));
        foreach (var outdatedModule in outdatedModules)
        {
            logger.LogInformation($"Updating {outdatedModule.ModuleName} from {localModules.First(rm => rm.ModuleId == outdatedModule.ModuleId).Version} to {outdatedModule.Version}");
            await moduleInstaller.InstallRemoteModule(outdatedModule);
        }

        logger.LogInformation("Initializing modules...");
        Dispatcher.UIThread.Invoke(() => libManager.Initialize());

        await Task.CompletedTask;
    }
}
