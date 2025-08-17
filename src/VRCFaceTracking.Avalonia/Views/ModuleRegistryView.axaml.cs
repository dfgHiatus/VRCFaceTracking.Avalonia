using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.DependencyInjection;
using VRCFaceTracking.Avalonia.Helpers;
using VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Avalonia.Views;

public partial class ModuleRegistryView : UserControl
{
    public ModuleRegistryView()
    {
        InitializeComponent();

        ModuleDataService = Ioc.Default.GetService<IModuleDataService>()!;
        ModuleInstaller = Ioc.Default.GetService<ModuleInstaller>()!;
        LibManager = Ioc.Default.GetService<ILibManager>()!;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        this.Get<Button>("BrowseLocal")!.Click += async delegate
        {
            var topLevel = TopLevel.GetTopLevel(this)!;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a .zip.",
                AllowMultiple = false,
                FileTypeFilter = [ZIP]
            });

            if (files.Count == 0) return;

            string? path = null;
            try
            {
                path = await ModuleInstaller.InstallLocalModule(files.First().Path.AbsolutePath);
            }
            finally
            {
                if (path != null)
                {
                    LocalModuleInstalled?.Invoke();
                    LibManager.Initialize();
                }
            }
        };

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private ModuleInstaller ModuleInstaller { get; }
    private IModuleDataService ModuleDataService { get; }
    private ILibManager LibManager { get; }


    private static FilePickerFileType ZIP { get; } = new("Zip Files")
    {
        Patterns = ["*.zip"]
    };

    public static event Action<InstallableTrackingModule>? ModuleSelected;
    public static event Action? LocalModuleInstalled;
    public static event Action<InstallableTrackingModule>? RemoteModuleInstalled;

    private void ReinitButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as ModuleRegistryViewModel;
        var init = viewModel.RequestReinit;
        viewModel.ModuleTryReinitialize();
    }

    private void DecrementOrder(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var module = button.DataContext as InstallableTrackingModule;

        if (module != null) module.Order--;
    }

    private void IncrementOrder(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var module = button.DataContext as InstallableTrackingModule;

        if (module != null) module.Order++;
    }

    private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        var viewModel = DataContext as ModuleRegistryViewModel;
        viewModel.DetachedFromVisualTree();
    }

    private void InstallButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ModuleList.ItemCount == 0) return;
        var index = ModuleList.SelectedIndex;
        if (index == -1) index = 0;
        if (ModuleList.Items[index] is not InstallableTrackingModule module) return;

        InstallButton.Content = "Please Restart VRCFT";
        InstallButton.IsEnabled = false;
        RemoteModuleInstalled?.Invoke(module);
        OnModuleSelected(ModuleList, null);
    }

    private void ModuleSelectionTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tabControl) return;

        var currentlySelectedItem = tabControl.SelectedContent;

        if (currentlySelectedItem is not Visual visual) return;

        var listBox = FindChild<ListBox>(visual);

        if (listBox == null) return;

        if (listBox.SelectedIndex == -1)
            listBox.SelectedIndex = 0;

        OnModuleSelected(listBox, null);
    }

    // Helper method to find a child control of a specific type
    private T FindChild<T>(Visual parent) where T : Visual
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is T result) return result;

            // Recursively search in child elements
            var foundChild = FindChild<T>(child);
            if (foundChild != null) return foundChild;
        }

        return null;
    }

    private void OnModuleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox moduleListBox) return;
        if (moduleListBox.ItemCount == 0) return;

        var index = moduleListBox.SelectedIndex;
        if (index == -1) index = 0;
        if (moduleListBox.Items[index] is not InstallableTrackingModule module) return;

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
            ModuleSelected?.Invoke(selectedModule);
    }

    public InstallableTrackingModule[] GetRemoteModules()
    {
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
            return null;
        }

        // Now comes the tricky bit, we get all locally installed modules and add them to the list.
        // If any of the IDs match a remote module and the other data contained within does not match,
        // then we need to set the local module install state to outdated. If everything matches then we need to set the install state to installed.
        var installedModules = ModuleDataService.GetInstalledModules();

        var localModules = new List<InstallableTrackingModule>(); // dw about it
        foreach (var installedModule in installedModules)
        {
            installedModule.InstallationState = InstallState.Installed;
            var remoteModule = remoteModules.FirstOrDefault(x => x.ModuleId == installedModule.ModuleId);
            if (remoteModule ==
                null) // If this module is completely missing from the remote list, then we need to add it to the list.
                // This module is installed but not in the remote list, so we need to add it to the list.
                localModules.Add(installedModule);
            else
                // This module is installed and in the remote list, so we need to update the remote module's install state.
                remoteModule.InstallationState = remoteModule.Version != installedModule.Version
                    ? InstallState.Outdated
                    : InstallState.Installed;
        }

        var remoteCount = remoteModules.Count();

        // Sort our data by name, then place dfg at the top of the list :3
        remoteModules = remoteModules.OrderByDescending(x => x.AuthorName == "dfgHiatus")
            .ThenBy(x => x.ModuleName);

        var modules = remoteModules.ToArray();
        var first = modules.First();
        ModuleList.SelectedIndex = 0;
        ModuleSelected?.Invoke(first);

        return modules;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var viewModel = DataContext as ModuleRegistryViewModel;
        viewModel.SetDropOverlay(true);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        var viewModel = DataContext as ModuleRegistryViewModel;
        viewModel.SetDropOverlay(false);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var viewModel = DataContext as ModuleRegistryViewModel;
        viewModel.SetDropOverlay(false);

        var items = e.Data.GetFiles();
        if (items == null) return;

        foreach (var file in items)
        {
            if (!file.Name.EndsWith(".zip"))
                continue;

            await InstallModule(file);
        }
    }

    private async Task InstallModule(IStorageItem file)
    {
        var path = string.Empty;
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

    private void ClearSearchBox_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ModuleRegistryViewModel vm)
            vm.SearchText = string.Empty;
    }

    private void OpenLogDirectory(object sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Invoke(() => { URLHelpers.OpenUrl(Core.Utils.CustomLibsDirectory); });
    }
}
