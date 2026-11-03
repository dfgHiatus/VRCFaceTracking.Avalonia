using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using VRCFaceTracking.Avalonia.Helpers;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Avalonia.Services;
using VRCFaceTracking.Avalonia.Views;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;

public partial class ModuleRegistryViewModel : ViewModelBase
{
    private readonly DropOverlayService _dropOverlayService;
    [ObservableProperty] private ModuleEntry _module;
    [ObservableProperty] private int _moduleRating;

    private readonly SemaphoreSlim _moduleRatingLock = new(1);

    [ObservableProperty] private bool _modulesDetected;

    [ObservableProperty] private bool _noRemoteModulesDetected;

    private ModuleEntry[] _registryInfos;

    private bool _requestReinit;

    [ObservableProperty] private string _searchText;

    public ModuleRegistryViewModel()
    {
        _moduleRegistryView = Ioc.Default.GetService<ModuleRegistryView>()!;
        _moduleDataService = Ioc.Default.GetService<ModuleDataService>()!;
        _moduleInstaller = Ioc.Default.GetService<ModuleInstaller>()!;
        _libManager = Ioc.Default.GetService<ILibManager>()!;
        _dropOverlayService = Ioc.Default.GetService<DropOverlayService>()!;

        ModuleRegistryView.ModuleSelected += ModuleSelected;
        ModuleRegistryView.LocalModuleInstalled += LocalModuleInstalled;
        ModuleRegistryView.RemoteModuleInstalled += RemoteModuleInstalled;

        _registryInfos = _moduleRegistryView.GetRemoteModules();

        ResetInstalledModulesList(true);

        InstalledModules.CollectionChanged += OnLocalModuleCollectionChanged;

        _noRemoteModulesDetected = _registryInfos == null;

        // Hide UI if the user has no remote modules (IE not internet connection) and no local modules
        _modulesDetected = !_noRemoteModulesDetected || InstalledModules.Count > 0;

        foreach (var module in _registryInfos) FilteredModuleInfos.Add(module);
    }

    public ObservableCollection<ModuleEntry> FilteredModuleInfos { get; } = [];
    public ObservableCollection<ModuleEntry> InstalledModules { get; set; } = [];
    private ModuleRegistryView _moduleRegistryView { get; }
    private ModuleDataService _moduleDataService { get; }
    private ModuleInstaller _moduleInstaller { get; }
    private ILibManager _libManager { get; }

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

    public int CorrectedModuleCount => Math.Max(0, InstalledModules.Count - 1);

    private void RequestReinitialize()
    {
        RequestReinit = true;
    }

    public async void ModuleTryReinitialize()
    {
        if (RequestReinit)
        {
            RequestReinit = false;
            var installedModules = InstalledModules.ToList();
            await _moduleDataService.SaveInstalledModulesDataAsync(installedModules);
            // Initialize tears down running modules first, then reloads them in the saved order.
            await _libManager.Initialize();
        }
    }

    private void ModuleRatingChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != "ModuleRating")
            return;

        // Can't rate local modules
        if (Module.IsLocal)
            return;

        _moduleDataService.SetMyRatingAsync(Module, ModuleRating);
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
        foreach (var module in filtered) FilteredModuleInfos.Add(module);
    }

    private void ModuleSelected(ModuleEntry module)
    {
        // Dispatcher.Post takes an Action, so this lambda is effectively async void: anything
        // that escapes it is rethrown on the dispatcher and terminates the whole process.
        // Ratings come from a remote registry, so every failure path has to be contained here.
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                Module = module;

                // Local modules don't have ratings
                if (Module.IsLocal)
                {
                    ModuleRating = 0;
                    return;
                }

                var myRating = await _moduleDataService.GetMyRatingAsync(module);

                // We use a lock here because without it a race condition occurres
                // when adding propertyChanged handlers.
                // a handler might be added from another async call before this one
                // finishes causing unwanted events firing.
                await _moduleRatingLock.WaitAsync();
                try
                {
                    // Check if the selected module is still the same one that we called
                    // GetMyRatingAsync for.
                    if (Module.ModuleId != module.ModuleId)
                        return;

                    // Prevent events from firing
                    PropertyChanged -= ModuleRatingChanged;
                    if (myRating != null)
                        ModuleRating = (int)myRating;
                    else
                        ModuleRating = 0;

                    // return the events back
                    PropertyChanged += ModuleRatingChanged;
                }
                finally
                {
                    _moduleRatingLock.Release();
                }
            }
            catch (Exception e)
            {
                // Registry unreachable or slow: show "not rated" instead of dying.
                Console.Error.WriteLine($"Failed to load rating for module '{module.ModuleId}': {e}");
                ModuleRating = 0;
            }
        });
    }

    private void LocalModuleInstalled()
    {
        _registryInfos = _moduleRegistryView.GetRemoteModules();

        ResetInstalledModulesList();

        FilteredModuleInfos.Clear();
        foreach (var module in _registryInfos) FilteredModuleInfos.Add(module);

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

    public async Task RemoteModuleInstalled(ModuleEntry module)
    {
        // Upstream's installer tears down and reinitializes modules itself, so no restart or reinit request is needed.
        switch (module.InstallationState)
        {
            case InstallState.NotInstalled or InstallState.Outdated:
                {
                    var path = await _moduleInstaller.InstallRemoteModule(module);
                    if (!string.IsNullOrEmpty(path))
                    {
                        module.InstallationState = InstallState.Installed;
                        await _moduleDataService.IncrementDownloadsAsync(module);
                        module.Downloads++;
                    }

                    break;
                }
            case InstallState.Installed:
                {
                    await _moduleInstaller.UninstallModule(module);
                    module.InstallationState = InstallState.NotInstalled;
                    break;
                }
        }

        ResetInstalledModulesList(suppressReinit: true);
    }

    private void ResetInstalledModulesList(bool suppressReinit = false)
    {
        var installedModules = _moduleDataService.GetInstalledModuleEntries();

        InstalledModules.Clear();
        var i = 0;
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
        if (sender is not ModuleEntry module)
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
        for (var i = 0; i < InstalledModules.Count; i++) InstalledModules[i].Order = i;
    }

    public void OpenModuleUrl()
    {
        URLHelpers.OpenUrl(_module.ModulePageUrl);
    }

    public void SetDropOverlay(bool show)
    {
        if (show) _dropOverlayService.Show();
        else _dropOverlayService.Hide();
    }

    public void DetachedFromVisualTree()
    {
        ModuleRegistryView.ModuleSelected -= ModuleSelected;
        _moduleDataService.SaveInstalledModulesDataAsync(InstalledModules);

        ModuleTryReinitialize();
    }
}
