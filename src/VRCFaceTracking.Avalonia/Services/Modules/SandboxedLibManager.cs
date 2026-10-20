#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Sandboxing;
using VRCFaceTracking.Core.Sandboxing.IPC;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Avalonia.Services.Modules;

/// <summary>
/// Desktop module host. Like upstream's UnifiedLibManager, each module runs in its own
/// VRCFaceTracking.ModuleProcess and talks to us over upstream's UDP IPC. Unlike upstream, processes are
/// started in parallel but initialized strictly in load order.
/// </summary>
public sealed class SandboxedLibManager : OrderedLibManagerBase
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger _moduleLogger;
    private readonly IOscTarget _oscTarget;
    private readonly SendCoordinator _sendCoordinator;
    private readonly string _sandboxProcessPath;
    private readonly object _modulesLock = new();
    private readonly List<SandboxModule> _modules = [];
    private VrcftSandboxServer? _sandboxServer;

    public SandboxedLibManager(
        ILoggerFactory loggerFactory,
        IDispatcherService dispatcherService,
        IModuleDataService moduleDataService,
        IOscTarget oscTarget,
        SendCoordinator sendCoordinator)
        : base(loggerFactory, dispatcherService, moduleDataService)
    {
        // Same category upstream uses for logs forwarded from module processes.
        _moduleLogger = loggerFactory.CreateLogger("\0VRCFT\0");
        _oscTarget = oscTarget;
        _sendCoordinator = sendCoordinator;

        // Upstream resolves this against the working directory; use the app directory so shortcuts and autostart work.
        var exeName = OperatingSystem.IsWindows() ? "VRCFaceTracking.ModuleProcess.exe" : "VRCFaceTracking.ModuleProcess";
        _sandboxProcessPath = Path.Combine(AppContext.BaseDirectory, exeName);
    }

    protected override Task PrepareModulesAsync(IReadOnlyList<string> modulePaths)
    {
        if (!File.Exists(_sandboxProcessPath))
        {
            Logger.LogError("Module sandbox process not found at {path}. No modules can be loaded.", _sandboxProcessPath);
            return Task.CompletedTask;
        }

        EnsureSandboxServer();

        foreach (var path in modulePaths)
        {
            // Hold the lock across Process.Start so a fast handshake can't arrive before the module is registered.
            lock (_modulesLock)
            {
                try
                {
                    var process = Process.Start(new ProcessStartInfo(
                        _sandboxProcessPath,
                        $"--port {_sandboxServer!.Port} --module-path \"{path}\" --parent-pid {Environment.ProcessId}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    });

                    if (process == null)
                    {
                        Logger.LogWarning("Failed to start sandbox process for {path}. Skipping...", path);
                        continue;
                    }

                    _modules.Add(new SandboxModule(path, process));
                    Logger.LogDebug("Started sandbox process {pid} for {path}", process.Id, path);
                }
                catch (Exception e)
                {
                    Logger.LogWarning("{error} Failed to start sandbox process for {path}. Skipping...", e.Message, path);
                }
            }
        }

        return Task.CompletedTask;
    }

    protected override async Task<ModuleInitResult?> TryInitializeModuleAsync(string modulePath, bool eyeAvailable, bool expressionAvailable)
    {
        SandboxModule? module;
        lock (_modulesLock)
        {
            module = _modules.FirstOrDefault(m => m.ModulePath == modulePath);
        }

        if (module == null)
        {
            return null;
        }

        if (!await WaitAsync(module.Connected.Task, HandshakeTimeout))
        {
            Logger.LogWarning("{module} did not connect within {timeout}s. Skipping...", module.ClassName, HandshakeTimeout.TotalSeconds);
            return null;
        }

        Logger.LogInformation("Initializing {module}...", module.ClassName);
        _sandboxServer!.SendData(new EventInitGetSupported(), module.Port);
        var supported = await WaitForAsync(module.Supported.Task, InitTimeout);
        if (supported == null)
        {
            Logger.LogWarning("{module} did not report its supported tracking types. Skipping...", module.ClassName);
            return null;
        }

        eyeAvailable &= supported.eyeAvailable;
        expressionAvailable &= supported.expressionAvailable;
        if (!eyeAvailable && !expressionAvailable)
        {
            Logger.LogDebug("{module} offers nothing that's still unclaimed. Skipping...", module.ClassName);
            return null;
        }

        _sandboxServer.SendData(new EventInitPacket { eyeAvailable = eyeAvailable, expressionAvailable = expressionAvailable }, module.Port);
        var init = await WaitForAsync(module.Initialized.Task, InitTimeout);
        if (init == null)
        {
            Logger.LogWarning("{module} did not finish initializing within {timeout}s. Skipping...", module.ClassName, InitTimeout.TotalSeconds);
            return null;
        }

        // A module may only claim what it was offered.
        var eye = init.eyeSuccess && eyeAvailable;
        var expression = init.expressionSuccess && expressionAvailable;
        Logger.LogInformation("Got init for module {module}. Eye: {eye} Expr: {expr}", module.ClassName, eye, expression);
        if (!eye && !expression)
        {
            return null;
        }

        var metadata = new ModuleMetadataInternal
        {
            Name = init.ModuleInformationName,
            StaticImages = init.IconDataStreams,
            UsingEye = eye,
            UsingExpression = expression,
            Active = true,
        };
        module.Claim(metadata);
        metadata.PropertyChanged += (_, args) => OnModuleMetadataChanged(module, args);

        _sendCoordinator.RegisterModule(module.Port);
        module.StartUpdatePump(_sandboxServer);
        return new ModuleInitResult(eye, expression, metadata);
    }

    protected override async Task ReleaseUnusedModulesAsync()
    {
        List<SandboxModule> unused;
        lock (_modulesLock)
        {
            unused = _modules.Where(m => m.Metadata == null).ToList();
            _modules.RemoveAll(unused.Contains);
        }

        await Task.WhenAll(unused.Select(TeardownModuleAsync));
    }

    protected override async Task TeardownModulesAsync()
    {
        List<SandboxModule> modules;
        lock (_modulesLock)
        {
            modules = [.. _modules];
            _modules.Clear();
        }

        await Task.WhenAll(modules.Select(TeardownModuleAsync));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sandboxServer?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void EnsureSandboxServer()
    {
        if (_sandboxServer != null)
        {
            return;
        }

        // Keep the sandbox off the OSC ports VRChat and we use.
        var reservedPorts = new[] { 9000, 9001, _oscTarget.InPort, _oscTarget.OutPort }.Distinct().ToArray();
        _sandboxServer = new VrcftSandboxServer(LoggerFactory, reservedPorts);
        _sandboxServer.OnPacketReceived += OnSandboxPacketReceived;
    }

    private void OnSandboxPacketReceived(in IpcPacket packet, in int port)
    {
        switch (packet.GetPacketType())
        {
            case IpcPacket.PacketType.Handshake:
                OnHandshake((HandshakePacket)packet, port);
                return;
            case IpcPacket.PacketType.EventLog:
                var log = (EventLogPacket)packet;
                _moduleLogger.Log(log.LogLevel, "{message}", log.Message);
                return;
        }

        var module = FindByPort(port);
        if (module == null)
        {
            return;
        }

        switch (packet.GetPacketType())
        {
            case IpcPacket.PacketType.ReplyGetSupported:
                module.Supported.TrySetResult((ReplySupportedPacket)packet);
                break;
            case IpcPacket.PacketType.ReplyInit:
                module.Initialized.TrySetResult((ReplyInitPacket)packet);
                break;
            case IpcPacket.PacketType.ReplyUpdate:
                OnReplyUpdate(module, (ReplyUpdatePacket)packet);
                break;
            case IpcPacket.PacketType.ReplyTeardown:
                module.TornDown.TrySetResult();
                break;
        }
    }

    private void OnHandshake(HandshakePacket handshake, int port)
    {
        SandboxModule? module;
        lock (_modulesLock)
        {
            module = _modules.FirstOrDefault(m => m.Process.Id == handshake.PID);
        }

        if (module == null)
        {
            Logger.LogDebug("Ignoring handshake from unknown sandbox process {pid}", handshake.PID);
            return;
        }

        module.Port = port;
        module.Connected.TrySetResult();
    }

    private void OnReplyUpdate(SandboxModule module, ReplyUpdatePacket reply)
    {
        var metadata = module.Metadata;
        if (metadata is not { Active: true })
        {
            return;
        }

        if (metadata.UsingEye)
        {
            reply.UpdateGlobalEyeState();
        }
        if (metadata.UsingExpression)
        {
            reply.UpdateGlobalExpressionState();
        }
        reply.UpdateHeadState();

        _sendCoordinator.NotifyReply(module.Port);
    }

    private void OnModuleMetadataChanged(SandboxModule module, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(ModuleMetadataInternal.Active)
                                   or nameof(ModuleMetadataInternal.UsingEye)
                                   or nameof(ModuleMetadataInternal.UsingExpression)))
        {
            return;
        }

        var metadata = module.Metadata!;
        _sandboxServer?.SendData(
            new EventStatusUpdatePacket
            {
                ModuleState = metadata.Active ? ModuleState.Active : ModuleState.Idle,
                UsingEye = metadata.UsingEye,
                UsingExpression = metadata.UsingExpression,
            },
            module.Port);
    }

    private SandboxModule? FindByPort(int port)
    {
        lock (_modulesLock)
        {
            return _modules.FirstOrDefault(m => m.Port == port);
        }
    }

    private async Task TeardownModuleAsync(SandboxModule module)
    {
        Logger.LogInformation("Tearing down {module}", module.ClassName);
        await module.StopUpdatePumpAsync();

        if (module.Port > 0)
        {
            _sendCoordinator.UnregisterModule(module.Port);
            _sandboxServer?.SendData(new EventTeardownPacket(), module.Port);
            await WaitAsync(module.TornDown.Task, TimeSpan.FromSeconds(2));
        }

        try
        {
            if (!module.Process.HasExited && !module.Process.WaitForExit(500))
            {
                Logger.LogDebug("Module {pid} didn't exit gracefully. Forcing kill...", module.Process.Id);
                module.Process.Kill(entireProcessTree: true);
                if (!module.Process.WaitForExit(2000))
                {
                    Logger.LogCritical("Process {pid} is a zombie or stuck in kernel I/O. Manual intervention required.", module.Process.Id);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to stop sandbox process for {module}", module.ClassName);
        }
        finally
        {
            module.Process.Dispose();
        }
    }

    private static async Task<bool> WaitAsync(Task task, TimeSpan timeout) =>
        await Task.WhenAny(task, Task.Delay(timeout)) == task;

    private static async Task<T?> WaitForAsync<T>(Task<T> task, TimeSpan timeout) where T : class =>
        await WaitAsync(task, timeout) ? task.Result : null;

    private sealed class SandboxModule(string modulePath, Process process)
    {
        private CancellationTokenSource? _pumpCts;
        private Thread? _pumpThread;

        public string ModulePath { get; } = modulePath;
        public string ClassName { get; } = Path.GetFileNameWithoutExtension(modulePath);
        public Process Process { get; } = process;
        public int Port { get; set; } = -1;
        public ModuleMetadataInternal? Metadata { get; private set; }

        public TaskCompletionSource Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ReplySupportedPacket> Supported { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ReplyInitPacket> Initialized { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource TornDown { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Claim(ModuleMetadataInternal metadata) => Metadata = metadata;

        // Ask the module for fresh data at 100Hz, as upstream does.
        public void StartUpdatePump(VrcftSandboxServer server)
        {
            var cts = new CancellationTokenSource();
            var port = Port;
            _pumpCts = cts;
            _pumpThread = new Thread(() =>
            {
                var updatePacket = new EventUpdatePacket();
                while (!cts.IsCancellationRequested)
                {
                    Thread.Sleep(10);
                    server.SendData(updatePacket, port);
                }
            })
            {
                IsBackground = true,
                Name = $"{ClassName} update pump",
            };
            _pumpThread.Start();
        }

        public async Task StopUpdatePumpAsync()
        {
            if (_pumpCts == null)
            {
                return;
            }

            await _pumpCts.CancelAsync();
            _pumpThread?.Join(500);
            _pumpCts.Dispose();
            _pumpCts = null;
        }
    }
}
