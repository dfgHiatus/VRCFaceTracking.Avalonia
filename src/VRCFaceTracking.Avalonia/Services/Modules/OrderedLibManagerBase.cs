#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Library;

namespace VRCFaceTracking.Avalonia.Services.Modules;

/// <summary>
/// Shared lifecycle for the front end's <see cref="ILibManager"/> implementations. Upstream's UnifiedLibManager
/// starts every module at once and lets whichever answers first claim eye/expression tracking; here modules are
/// initialized one at a time in the user's load order, and only offered the tracking types still unclaimed.
/// </summary>
public abstract class OrderedLibManagerBase : ILibManager, IDisposable
{
    private static readonly PropertyInfo EyeStatusProperty =
        typeof(UnifiedLibManager).GetProperty(nameof(UnifiedLibManager.EyeStatus))!;
    private static readonly PropertyInfo ExpressionStatusProperty =
        typeof(UnifiedLibManager).GetProperty(nameof(UnifiedLibManager.ExpressionStatus))!;

    private readonly IDispatcherService _dispatcherService;
    private readonly IModuleDataService _moduleDataService;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private ModuleMetadataInternal? _placeholder;

    protected ILoggerFactory LoggerFactory { get; }
    protected ILogger Logger { get; }

    protected OrderedLibManagerBase(ILoggerFactory loggerFactory, IDispatcherService dispatcherService, IModuleDataService moduleDataService)
    {
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger(GetType());
        _dispatcherService = dispatcherService;
        _moduleDataService = moduleDataService;
    }

    public ObservableCollection<ModuleMetadataInternal> LoadedModulesMetadata { get; set; } = [];

    /// <summary>Result of initializing a single module. Null from <see cref="TryInitializeModuleAsync"/> means it failed outright.</summary>
    protected sealed record ModuleInitResult(bool Eye, bool Expression, ModuleMetadataInternal Metadata);

    public Task Initialize() => Task.Run(async () =>
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            ShowPlaceholder("Initializing Modules...");
            await TeardownModulesAsync();
            SetTrackingStatus(eyeClaimed: false, expressionClaimed: false);

            // ModuleDataService returns modules already sorted by the user's load order.
            var modulePaths = _moduleDataService.GetInstalledModules()
                .Where(m => m is not ModuleEntry { Instantiatable: false } && !string.IsNullOrEmpty(m.AssemblyLoadPath))
                .Select(m => m.AssemblyLoadPath)
                .ToList();

            Logger.LogInformation("Initializing {count} enabled module(s) in load order", modulePaths.Count);
            if (await InitializeInOrderAsync(modulePaths) == 0)
            {
                Logger.LogWarning("No modules loaded.");
                ShowPlaceholder("No Modules Loaded");
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Module initialization failed");
            ShowPlaceholder("No Modules Loaded");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    });

    public async Task TeardownAllModules()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            Logger.LogInformation("Tearing down all modules...");
            await TeardownModulesAsync();
            SetTrackingStatus(eyeClaimed: false, expressionClaimed: false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lifecycleLock.Dispose();
        }
    }

    private async Task<int> InitializeInOrderAsync(List<string> modulePaths)
    {
        if (modulePaths.Count == 0)
        {
            return 0;
        }

        await PrepareModulesAsync(modulePaths);

        bool eyeClaimed = false, expressionClaimed = false;
        var initialized = 0;
        foreach (var path in modulePaths)
        {
            if (eyeClaimed && expressionClaimed)
            {
                break;
            }

            var result = await TryInitializeModuleAsync(path, eyeAvailable: !eyeClaimed, expressionAvailable: !expressionClaimed);
            if (result is null || (!result.Eye && !result.Expression))
            {
                continue;
            }

            eyeClaimed |= result.Eye;
            expressionClaimed |= result.Expression;
            SetTrackingStatus(eyeClaimed, expressionClaimed);
            Publish(result.Metadata);
            initialized++;

            Logger.LogInformation("Tracking initialized via {module} (eye: {eye}, expression: {expression})",
                result.Metadata.Name, result.Eye, result.Expression);
        }

        await ReleaseUnusedModulesAsync();
        return initialized;
    }

    /// <summary>Called once before modules are initialized in order, e.g. to start sandbox processes in parallel.</summary>
    protected virtual Task PrepareModulesAsync(IReadOnlyList<string> modulePaths) => Task.CompletedTask;

    /// <summary>Initialize one module, offering it only the tracking types that are still unclaimed.</summary>
    protected abstract Task<ModuleInitResult?> TryInitializeModuleAsync(string modulePath, bool eyeAvailable, bool expressionAvailable);

    /// <summary>Called after the ordered pass, to release modules that were prepared but never claimed anything.</summary>
    protected virtual Task ReleaseUnusedModulesAsync() => Task.CompletedTask;

    /// <summary>Stop and release every running module.</summary>
    protected abstract Task TeardownModulesAsync();

    // Core's OSC parameters (EyeTrackingActive, ExpressionTrackingActive) read these statics, and only
    // UnifiedLibManager can set them, so we set them through reflection on its behalf.
    private static void SetTrackingStatus(bool eyeClaimed, bool expressionClaimed)
    {
        EyeStatusProperty.SetValue(null, eyeClaimed ? ModuleState.Active : ModuleState.Uninitialized);
        ExpressionStatusProperty.SetValue(null, expressionClaimed ? ModuleState.Active : ModuleState.Uninitialized);
    }

    private void ShowPlaceholder(string text)
    {
        var placeholder = new ModuleMetadataInternal { Active = false, Name = text };
        _dispatcherService.Run(() =>
        {
            _placeholder = placeholder;
            LoadedModulesMetadata.Clear();
            LoadedModulesMetadata.Add(placeholder);
        });
    }

    private void Publish(ModuleMetadataInternal metadata)
    {
        _dispatcherService.Run(() =>
        {
            if (_placeholder != null)
            {
                LoadedModulesMetadata.Remove(_placeholder);
                _placeholder = null;
            }

            LoadedModulesMetadata.Add(metadata);
        });
    }
}
