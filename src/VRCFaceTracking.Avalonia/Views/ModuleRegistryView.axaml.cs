using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Avalonia.Views;

public partial class ModuleRegistryView : UserControl
{
    public static event Action<InstallableTrackingModule>? ModuleSelected;
    public static event Action? LocalModuleInstalled;
    public static event Action<InstallableTrackingModule>? RemoteModuleInstalled;
    public static event Action<bool> DragDetected;

    private ModuleInstaller ModuleInstaller { get; }
    private IModuleDataService ModuleDataService { get; }
    private ILibManager LibManager { get; set; }

    public ListBox _moduleList;

    private static FilePickerFileType ZIP { get; } = new("Zip Files")
    {
        Patterns = [ "*.zip" ]
    };

    public ModuleRegistryView()
    {
        InitializeComponent();

        ModuleDataService = Ioc.Default.GetService<IModuleDataService>()!;
        ModuleInstaller = Ioc.Default.GetService<ModuleInstaller>()!;
        LibManager = Ioc.Default.GetService<ILibManager>()!;

        _moduleList = this.Get<ListBox>("ModuleList")!;
        this.Get<Button>("BrowseLocal")!.Click += async delegate
        {
            var topLevel = TopLevel.GetTopLevel(this)!;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a .zip.",
                AllowMultiple = true,
                FileTypeFilter = [ZIP]
            });

            foreach (var file in files)
            {
                await InstallModule(file);
            }
        };

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void InstallButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_moduleList.ItemCount == 0) return;
        var index = _moduleList.SelectedIndex;
        if (index == -1) index = 0;
        if (_moduleList.Items[index] is not InstallableTrackingModule module) return;

        InstallButton.Content = "Please Restart VRCFT";
        InstallButton.IsEnabled = false;
        RemoteModuleInstalled?.Invoke(module);
    }

    private void OnModuleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_moduleList.ItemCount == 0) return;
        var index = _moduleList.SelectedIndex;
        if (index == -1) index = 0;
        if (_moduleList.Items[index] is not InstallableTrackingModule module) return;

        switch (module.InstallationState)
        {
            case InstallState.NotInstalled or InstallState.Outdated:
            {
                InstallButton.Content = "Install";
                InstallButton.IsEnabled = true;
                break;
            }
            case InstallState.Installed:
            {
                InstallButton.Content = "Uninstall";
                InstallButton.IsEnabled = true;
                break;
            }
        }

        if (sender is ListBox listBox && listBox.SelectedItem is InstallableTrackingModule selectedModule)
        {
            ModuleSelected?.Invoke(selectedModule);
        }
    }

    public InstallableTrackingModule[] GetModules(out (int localCount, int remoteCount) moduleCounts)
    {
        IEnumerable<InstallableTrackingModule> installedModules = ModuleDataService.GetInstalledModules();
        IEnumerable<InstallableTrackingModule> remoteModules = [];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            Task.Run(async () =>
            {
                remoteModules = await ModuleDataService.GetRemoteModules()
                    .ConfigureAwait(false);
            }, cts.Token).Wait(cts.Token);
        }
        catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
        {
            // Timeout occurred, just return installed modules (if any)
            var arr = installedModules.ToArray();
            moduleCounts.remoteCount = 0;
            moduleCounts.localCount = arr.Length;
            return arr;
        }

        // If we didn't timeout, but we didn't get anything from the module manifest, AND we don't have any installed modules, return nothing
        if (remoteModules.Count() == 0 && installedModules.Count() == 0)
        {
            moduleCounts.remoteCount = 0;
            moduleCounts.localCount = 0;
            return [];
        }

        var localModules = new List<InstallableTrackingModule>();    // dw about it
        foreach (var installedModule in installedModules)
        {
            installedModule.InstallationState = InstallState.Installed;
            var remoteModule = remoteModules.FirstOrDefault(x => x.ModuleId == installedModule.ModuleId);
            if (remoteModule == null)   // If this module is completely missing from the remote list, then we need to add it to the list.
            {
                // This module is installed but not in the remote list, so we need to add it to the list.
                localModules.Add(installedModule);
            }
            else
            {
                // This module is installed and in the remote list, so we need to update the remote module's install state.
                remoteModule.InstallationState = remoteModule.Version != installedModule.Version ? InstallState.Outdated : InstallState.Installed;
            }
        }

        var remoteCount = remoteModules.Count();

        // Sort our data by name, then place dfg at the top of the list :3
        remoteModules = remoteModules.OrderByDescending(x => x.InstallationState == InstallState.Installed)
            .ThenByDescending(x => x.AuthorName == "dfgHiatus")
            .ThenBy(x => x.ModuleName);

        // Then prepend the local modules to the list.
        remoteModules = localModules.Concat(remoteModules);

        var modules = remoteModules.ToArray();
        var first = modules.First();
        _moduleList.SelectedIndex = 0;
        ModuleSelected?.Invoke(first);

        moduleCounts.localCount = installedModules.Count();
        moduleCounts.remoteCount = remoteCount;
        return modules;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        DragDetected?.Invoke(true);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DragDetected?.Invoke(false);
    }

    private async void OnDrop(object? sender, DragEventArgs e )
    {
        var items = e.Data.GetFiles();
        if (items == null) return;

        foreach (var file in items)
        {
            if (!file.Name.EndsWith(".zip"))
                continue;

            await InstallModule(file);
        }

        DragDetected?.Invoke(false);
    }

    private async Task InstallModule(IStorageItem file)
    {
        string path = string.Empty;
        try
        {
            path = await ModuleInstaller.InstallLocalModule(file.Path.AbsolutePath);
        }
        finally
        {
            if (!string.IsNullOrEmpty(path))
            {
                BrowseLocalText.Text = "Successfully installed module(s).";
                LocalModuleInstalled?.Invoke();
                LibManager.Initialize();
            }
            else
            {
                BrowseLocalText.Text = "Failed to install module(s). Check logs for more information.";
            }
        }
    }
}

