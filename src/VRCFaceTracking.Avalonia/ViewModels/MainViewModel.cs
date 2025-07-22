using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VRCFaceTracking.Avalonia.Assets;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Avalonia.Services;
using VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;

namespace VRCFaceTracking.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DropOverlayService _dropOverlayService;
    public MainViewModel(IMessenger messenger)
    {
        Items = new ObservableCollection<ListItemTemplate>(_templates);
        SelectedListItem = Items.First(vm => vm.ModelType == typeof(HomePageViewModel));
        _dropOverlayService = Ioc.Default.GetService<DropOverlayService>();

        _dropOverlayService.ShowOverlayChanged += SetOverlay;

    }

    private void SetOverlay(bool show)
    {
        IsDropOverlayVisible = show;
    }

    private readonly List<ListItemTemplate> _templates =
    [
        new ListItemTemplate(typeof(HomePageViewModel), "HomeRegular", Resources.Shell_Main_Content),
        new ListItemTemplate(typeof(OutputPageViewModel), "TextFirstLineRegular", Resources.Shell_Output_Content),
        new ListItemTemplate(typeof(ModuleRegistryViewModel), "ArrowDownloadRegular", Resources.Shell_ModuleRegistry_Content),
        new ListItemTemplate(typeof(MutatorPageViewModel), "EditRegular", Resources.Shell_TrackingSettings_Content),
        new ListItemTemplate(typeof(SettingsPageViewModel), "SettingsRegular", "Settings"),
    ];

    public MainViewModel() : this(new WeakReferenceMessenger()) { }

    [ObservableProperty]
    private bool _isPaneOpen;
    [ObservableProperty]
    private bool _isDropOverlayVisible;

    [ObservableProperty]
    private ViewModelBase _currentPage = new HomePageViewModel();

    [ObservableProperty]
    private ListItemTemplate? _selectedListItem;

    partial void OnSelectedListItemChanged(ListItemTemplate? value)
    {
        if (value is null) return;

        var vm = Design.IsDesignMode
            ? Activator.CreateInstance(value.ModelType)
            : Ioc.Default.GetService(value.ModelType);

        if (vm is not ViewModelBase vmb) return;

        CurrentPage = vmb;
    }

    public ObservableCollection<ListItemTemplate> Items { get; }

    [RelayCommand]
    private void TriggerPane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    public void DetachedFromVisualTree()
    {
        _dropOverlayService.Hide();
        _dropOverlayService.ShowOverlayChanged -= SetOverlay;
    }
}
