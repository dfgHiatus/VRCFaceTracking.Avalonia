using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using VRCFaceTracking.Contracts.Services;

namespace VRCFaceTracking.Avalonia.Views;

public partial class SettingsPageView : UserControl
{
    public bool IsRiskySettingsEnabled { get; set; } = false;

    public bool IsAutoStartEnabled { get; set; } = false;

    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ComboBox _themeComboBox;

    public SettingsPageView()
    {
        InitializeComponent();

        _themeSelectorService = Ioc.Default.GetService<IThemeSelectorService>()!;
        _themeComboBox = this.Find<ComboBox>("ThemeCombo")!;
        _themeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
    }

    ~SettingsPageView()
    {
        _themeComboBox.SelectionChanged -= ThemeComboBox_SelectionChanged;
    }

    private void ThemeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        string theme = ((ComboBoxItem)_themeComboBox.SelectedItem!)!.Content!.ToString()!;
        ThemeVariant variant = ThemeVariant.Default;
        switch (theme)
        {
            case "System":
                variant = ThemeVariant.Default;
                break;
            case "Light":
                variant = ThemeVariant.Light;
                break;
            case "Dark":
                variant = ThemeVariant.Dark;
                break;
        }
        Dispatcher.UIThread.InvokeAsync(async () => await _themeSelectorService.SetThemeAsync(variant));
    }

    private void AutoStartToggled(object? sender, RoutedEventArgs e)
    {
        IsAutoStartEnabled = !IsAutoStartEnabled;
    }

    private void RiskySettingsToggled(object? sender, RoutedEventArgs e)
    {
        IsRiskySettingsEnabled = !IsRiskySettingsEnabled;
    }

    private void ReInit_OnClick(object? sender, RoutedEventArgs e)
    {

    }

    private void Reset_OnClick(object? sender, RoutedEventArgs e)
    {
        
    }
}

