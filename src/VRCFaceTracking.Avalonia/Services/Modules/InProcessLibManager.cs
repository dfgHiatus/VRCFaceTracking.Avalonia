#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Avalonia.Services.Modules;

/// <summary>
/// Mobile module host. Android and iOS can't spawn upstream's VRCFaceTracking.ModuleProcess, so modules are
/// loaded into collectible <see cref="AssemblyLoadContext"/>s inside the app and write to
/// <see cref="UnifiedTracking.Data"/> directly, as VRCFaceTracking did before sandboxing.
/// </summary>
public sealed class InProcessLibManager : OrderedLibManagerBase
{
    private readonly SendCoordinator _sendCoordinator;
    private readonly List<InProcessModule> _modules = [];

    // SendCoordinator tracks modules by sandbox port; in-process modules get negative ids so they can't collide.
    private int _nextCoordinatorId;

    public InProcessLibManager(
        ILoggerFactory loggerFactory,
        IDispatcherService dispatcherService,
        IModuleDataService moduleDataService,
        SendCoordinator sendCoordinator)
        : base(loggerFactory, dispatcherService, moduleDataService)
    {
        _sendCoordinator = sendCoordinator;
    }

    protected override Task<ModuleInitResult?> TryInitializeModuleAsync(string modulePath, bool eyeAvailable, bool expressionAvailable)
    {
        var className = Path.GetFileNameWithoutExtension(modulePath);
        var loadContext = new AssemblyLoadContext(modulePath, isCollectible: true);

        ExtTrackingModule module;
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(modulePath);
            var moduleType = assembly.GetTypes().FirstOrDefault(t => !t.IsAbstract && t.IsSubclassOf(typeof(ExtTrackingModule)));
            if (moduleType == null)
            {
                Logger.LogWarning("{module} has no ExtTrackingModule implementation. Skipping...", className);
                loadContext.Unload();
                return Task.FromResult<ModuleInitResult?>(null);
            }

            module = (ExtTrackingModule)Activator.CreateInstance(moduleType)!;
            module.Logger = LoggerFactory.CreateLogger(moduleType.Name);
        }
        catch (Exception e)
        {
            Logger.LogWarning("{error} Assembly {module} not able to be loaded. Skipping...", e.Message, className);
            loadContext.Unload();
            return Task.FromResult<ModuleInitResult?>(null);
        }

        Logger.LogInformation("Initializing {module}...", className);
        bool eye, expression;
        try
        {
            var supported = module.Supported;
            eyeAvailable &= supported.SupportsEye;
            expressionAvailable &= supported.SupportsExpression;
            if (!eyeAvailable && !expressionAvailable)
            {
                Logger.LogDebug("{module} offers nothing that's still unclaimed. Skipping...", className);
                loadContext.Unload();
                return Task.FromResult<ModuleInitResult?>(null);
            }

            var (eyeSuccess, expressionSuccess) = module.Initialize(eyeAvailable, expressionAvailable);
            eye = eyeSuccess && eyeAvailable;
            expression = expressionSuccess && expressionAvailable;
        }
        catch (MissingMethodException)
        {
            Logger.LogError("{module} does not properly implement ExtTrackingModule. Skipping...", className);
            loadContext.Unload();
            return Task.FromResult<ModuleInitResult?>(null);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Exception initializing {module}. Skipping...", className);
            loadContext.Unload();
            return Task.FromResult<ModuleInitResult?>(null);
        }

        Logger.LogInformation("Got init for module {module}. Eye: {eye} Expr: {expr}", className, eye, expression);
        if (!eye && !expression)
        {
            TryTeardown(module, className);
            loadContext.Unload();
            return Task.FromResult<ModuleInitResult?>(null);
        }

        module.Status = ModuleState.Active;
        module.ModuleInformation.Active = true;
        module.ModuleInformation.UsingEye = eye;
        module.ModuleInformation.UsingExpression = expression;

        var metadata = new ModuleMetadataInternal
        {
            Name = module.ModuleInformation.Name ?? className,
            StaticImages = module.ModuleInformation.StaticImages,
            UsingEye = eye,
            UsingExpression = expression,
            Active = true,
        };

        var running = new InProcessModule(module, className, loadContext, metadata, --_nextCoordinatorId);
        metadata.PropertyChanged += (_, args) => OnModuleMetadataChanged(running, args);
        _modules.Add(running);

        _sendCoordinator.RegisterModule(running.CoordinatorId);
        running.Start(_sendCoordinator, Logger);

        return Task.FromResult<ModuleInitResult?>(new ModuleInitResult(eye, expression, metadata));
    }

    protected override async Task TeardownModulesAsync()
    {
        var modules = _modules.ToList();
        _modules.Clear();

        foreach (var module in modules)
        {
            Logger.LogInformation("Tearing down {module}", module.ClassName);
            _sendCoordinator.UnregisterModule(module.CoordinatorId);
            await module.StopAsync(Logger);
            TryTeardown(module.Module, module.ClassName);
            module.LoadContext.Unload();
            module.Dispose();
        }
    }

    // Mirror UI toggles onto the module, as upstream's module process does for EventStatusUpdate.
    private static void OnModuleMetadataChanged(InProcessModule running, PropertyChangedEventArgs args)
    {
        var metadata = running.Metadata;
        switch (args.PropertyName)
        {
            case nameof(ModuleMetadataInternal.Active):
                running.Module.Status = metadata.Active ? ModuleState.Active : ModuleState.Idle;
                break;
            case nameof(ModuleMetadataInternal.UsingEye):
                running.Module.ModuleInformation.UsingEye = metadata.UsingEye;
                break;
            case nameof(ModuleMetadataInternal.UsingExpression):
                running.Module.ModuleInformation.UsingExpression = metadata.UsingExpression;
                break;
        }
    }

    private void TryTeardown(ExtTrackingModule module, string className)
    {
        try
        {
            module.Teardown();
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "{module} failed to cleanly shut down", className);
        }
    }

    private sealed class InProcessModule(
        ExtTrackingModule module,
        string className,
        AssemblyLoadContext loadContext,
        ModuleMetadataInternal metadata,
        int coordinatorId) : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private Thread? _updateThread;
        private Thread? _notifyThread;

        public ExtTrackingModule Module { get; } = module;
        public string ClassName { get; } = className;
        public AssemblyLoadContext LoadContext { get; } = loadContext;
        public ModuleMetadataInternal Metadata { get; } = metadata;
        public int CoordinatorId { get; } = coordinatorId;

        public void Start(SendCoordinator sendCoordinator, ILogger logger)
        {
            var token = _cts.Token;

            // Background so a module blocking in native code can't keep the app alive.
            _updateThread = new Thread(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Module.Update();
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "{module} threw during Update", ClassName);
                        Thread.Sleep(100);
                    }
                }
            })
            {
                IsBackground = true,
                Name = $"{ClassName} update",
            };

            // Modules update UnifiedTracking.Data in place; tell the coordinator there's fresh data at 100Hz,
            // the same rate upstream polls sandboxed modules at.
            _notifyThread = new Thread(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(10);
                    if (Metadata.Active)
                    {
                        sendCoordinator.NotifyReply(CoordinatorId);
                    }
                }
            })
            {
                IsBackground = true,
                Name = $"{ClassName} notify",
            };

            _updateThread.Start();
            _notifyThread.Start();
        }

        public async Task StopAsync(ILogger logger)
        {
            await _cts.CancelAsync();
            _notifyThread?.Join(500);
            if (_updateThread is { IsAlive: true } && !_updateThread.Join(TimeSpan.FromSeconds(2)))
            {
                logger.LogWarning("{module}'s update thread didn't stop in time", ClassName);
            }
        }

        public void Dispose() => _cts.Dispose();
    }
}
