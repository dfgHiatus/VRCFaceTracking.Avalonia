﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;

namespace VRCFaceTracking.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel(IMessenger messenger)
    {
        Items = new ObservableCollection<ListItemTemplate>(_templates);

        SelectedListItem = Items.First(vm => vm.ModelType == typeof(HomePageViewModel));
    }

    private readonly List<ListItemTemplate> _templates =
    [
        new ListItemTemplate(typeof(HomePageViewModel), "HomeRegular", "Home"),
        new ListItemTemplate(typeof(OutputPageViewModel), "TextFirstLineRegular", "Output"),
        new ListItemTemplate(typeof(ModuleRegistryViewModel), "ArrowDownloadRegular", "Module Registry"),
        new ListItemTemplate(typeof(SettingsPageViewModel), "SettingsRegular", "Settings"),
    ];

    public MainViewModel() : this(new WeakReferenceMessenger()) { }

    [ObservableProperty]
    private bool _isPaneOpen;

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
}
