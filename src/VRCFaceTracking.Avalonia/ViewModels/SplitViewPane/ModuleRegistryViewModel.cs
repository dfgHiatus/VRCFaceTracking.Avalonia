using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Avalonia.Views;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;

public partial class ModuleRegistryViewModel : ViewModelBase
{
    [ObservableProperty] private InstallableTrackingModule _module;

    [ObservableProperty] private string _searchText;

    [ObservableProperty] private bool _noRemoteModulesDetected;

    [ObservableProperty] private bool _modulesDetected;
    public ObservableCollection<InstallableTrackingModule> FilteredModuleInfos { get; } = [];
    public ObservableCollection<InstallableTrackingModule> InstalledModules { get; set; } = [];

    private InstallableTrackingModule[] _registryInfos;
    private ModuleRegistryView _moduleRegistryView { get; }
    private IModuleDataService _moduleDataService { get; }
    private ModuleInstaller _moduleInstaller { get; }
    private ILibManager _libManager { get; }

    private bool _requestReinit;
    public bool RequestReinit
    {
        get => _requestReinit;
        set
        {
            if (_requestReinit != value)
            {
                _requestReinit = value;
                OnPropertyChanged();
            }
        }
    }

    public int CorrectedModuleCount => Math.Clamp(InstalledModules.Count - 1, 0, InstalledModules.Count - 1);

    private void RequestReinitialize()
    {
        RequestReinit = true;
    }

    public void ModuleTryReinitialize()
    {
        if (RequestReinit)
        {
            RequestReinit = false;
            var installedModules = InstalledModules.ToList();
            _moduleDataService.SaveInstalledModulesDataAsync(installedModules);
            _libManager.TeardownAllAndResetAsync();
            _libManager.Initialize();
        }
    }

    public ModuleRegistryViewModel()
    {
        _moduleRegistryView = Ioc.Default.GetService<ModuleRegistryView>()!;
        _moduleDataService = Ioc.Default.GetService<IModuleDataService>()!;
        _moduleInstaller = Ioc.Default.GetService<ModuleInstaller>()!;
        _libManager = Ioc.Default.GetService<ILibManager>()!;
        ModuleRegistryView.ModuleSelected += ModuleSelected;
        ModuleRegistryView.LocalModuleInstalled += LocalModuleInstalled;
        ModuleRegistryView.RemoteModuleInstalled += RemoteModuleInstalled;

        _registryInfos = _moduleRegistryView.GetRemoteModules();

        ResetInstalledModulesList(suppressReinit: true);

        InstalledModules.CollectionChanged += OnLocalModuleCollectionChanged;

        _noRemoteModulesDetected = _registryInfos == null;

        // Hide UI if the user has no remote modules (IE not internet connection) and no local modules
        _modulesDetected = !_noRemoteModulesDetected || InstalledModules.Count > 0;

        foreach (var module in _registryInfos)
        {
            FilteredModuleInfos.Add(module);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        UpdateFilteredModules();
    }

    private void UpdateFilteredModules()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _registryInfos
            : _registryInfos.Where(m =>
                m.ModuleName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                m.DllFileName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                m.AuthorName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                m.ModuleDescription.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));

        FilteredModuleInfos.Clear();
        foreach (var module in filtered)
        {
            FilteredModuleInfos.Add(module);
        }
    }

    private void ModuleSelected(InstallableTrackingModule module)
    {
        Module = module;
    }

    private void LocalModuleInstalled()
    {
        _registryInfos = _moduleRegistryView.GetRemoteModules();

        ResetInstalledModulesList();

        FilteredModuleInfos.Clear();
        foreach (var module in _registryInfos)
        {
            FilteredModuleInfos.Add(module);
        }

        ModulesDetected = true;
    }

    public async void OnLocalModuleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InstalledModules.CollectionChanged -= OnLocalModuleCollectionChanged;

        RenumberModules();

        try
        {
            var _installedModules = InstalledModules.ToList(); // Create a copy to avoid modification during save
            await _moduleDataService.SaveInstalledModulesDataAsync(_installedModules);
            RequestReinitialize();
        }
        finally
        {
            // Re-enable the event handler
            InstalledModules.CollectionChanged += OnLocalModuleCollectionChanged;
        }
    }

    public async void RemoteModuleInstalled(InstallableTrackingModule module)
    {
        switch (module.InstallationState)
        {
            case InstallState.NotInstalled or InstallState.Outdated:
            {
                var path = await _moduleInstaller.InstallRemoteModule(module);
                if (!string.IsNullOrEmpty(path))
                {
                    module!.InstallationState = InstallState.Installed;
                    await _moduleDataService.IncrementDownloadsAsync(module);
                    module!.Downloads++;
                }
                break;
            }
            case InstallState.Installed:
            {
                _moduleInstaller.MarkModuleForDeletion(module);
                break;
            }
        }
        ResetInstalledModulesList();
    }

    private void ResetInstalledModulesList(bool suppressReinit = false)
    {
        var installedModules = _moduleDataService.GetInstalledModules()
                                                 .Where(m => m.InstallationState != InstallState.AwaitingRestart);

        InstalledModules.Clear();
        int i = 0;
        foreach (var installedModule in installedModules)
        {
            installedModule.PropertyChanged += OnLocalModulePropertyChanged;
            InstalledModules.Add(installedModule);
        }
        RenumberModules();
        if (!suppressReinit)
            RequestReinitialize();
    }

    private async void OnLocalModulePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is not InstallableTrackingModule module)
            return;

        var desiredIndex = module.Order;
        var currentIndex = InstalledModules.IndexOf(module);

        if (desiredIndex >= 0 && desiredIndex < InstalledModules.Count)
            InstalledModules.Move(currentIndex, desiredIndex);

        RenumberModules();

        var _installedModules = InstalledModules.ToList();
        await _moduleDataService.SaveInstalledModulesDataAsync(_installedModules);
    }

    private void RenumberModules()
    {
        for (int i = 0; i < InstalledModules.Count; i++)
        {
            InstalledModules[i].Order = i;
        }
    }

    public void OpenModuleUrl()
    {
        OpenUrl(_module.ModulePageUrl);
    }

    private void OpenUrl(string URL)
    {
        try
        {
            Process.Start(URL);
        }
        catch
        {

            var url = URL.Replace("&", "^&");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    public void DetachedFromVisualTree()
    {
        ModuleRegistryView.ModuleSelected -= ModuleSelected;
        _moduleDataService.SaveInstalledModulesDataAsync(InstalledModules);

        ModuleTryReinitialize();
    }
}
