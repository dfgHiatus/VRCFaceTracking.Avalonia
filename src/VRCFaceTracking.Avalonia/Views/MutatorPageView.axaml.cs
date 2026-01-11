using System;
using System.Collections.Specialized;
using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Contracts.Services;

namespace VRCFaceTracking.Avalonia.Views;

public partial class MutatorPageView : UserControl
{
    public MutatorPageViewModel ViewModel
    {
        get;
    }

    private readonly ILanguageSelectorService _languageSelectorService;

    public MutatorPageView()
    {
        ViewModel = Ioc.Default.GetService<MutatorPageViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        _languageSelectorService = Ioc.Default.GetService<ILanguageSelectorService>()!;
        _languageSelectorService.LanguageChanged += OnLanguageChanged;

        var header = this.FindControl<TextBlock>("HeaderTextBlock");
        if (header != null)
            header.Text = Assets.Resources.Shell_TrackingSettings_Content;

        // 强制刷新列表绑定一次，确保转换器在首次显示时使用当前语言
        RefreshItems();
    }

    private void RefreshItems()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var repeater = this.FindControl<ItemsRepeater>("MutationsListView");
            if (repeater != null)
            {
                // force rebind so value converters re-evaluate when language changes
                var src = ViewModel?.Mutations;
                repeater.ItemsSource = null;
                repeater.ItemsSource = src;
            }
        });
    }

    ~MutatorPageView()
    {
        if (_languageSelectorService != null)
            _languageSelectorService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var header = this.FindControl<TextBlock>("HeaderTextBlock");
            if (header != null)
                header.Text = Assets.Resources.Shell_TrackingSettings_Content;
            // 刷新 ItemsRepeater，使绑定的转换器重新计算显示文本
            RefreshItems();
        });
    }

    
}

// 将转换器置于文件级并公开，使 XAML 可以解析
public class MutationHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is VRCFaceTracking.Core.Params.Data.Mutation.TrackingMutation m)
            {
                var typeName = m.GetType().Name;
                var headerKey = $"{typeName}Settings.Header";
                var localizedHeader = Assets.Resources.ResourceManager.GetString(headerKey, Assets.Resources.Culture);
                if (!string.IsNullOrEmpty(localizedHeader)) return localizedHeader;
                var byName = Assets.Resources.ResourceManager.GetString(m.Name, Assets.Resources.Culture);
                return !string.IsNullOrEmpty(byName) ? byName : m.Name;
            }

            if (value is string s)
            {
                var byName = Assets.Resources.ResourceManager.GetString(s, Assets.Resources.Culture);
                return !string.IsNullOrEmpty(byName) ? byName : s;
            }
        }
        catch { }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class MutationDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is VRCFaceTracking.Core.Params.Data.Mutation.TrackingMutation m)
            {
                var typeName = m.GetType().Name;
                var descKey = $"{typeName}Settings.Description";
                var localizedDesc = Assets.Resources.ResourceManager.GetString(descKey, Assets.Resources.Culture);
                return !string.IsNullOrEmpty(localizedDesc) ? localizedDesc : m.Description ?? string.Empty;
            }
        }
        catch { }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class ComponentTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string s)
            {
                var localized = Assets.Resources.ResourceManager.GetString(s, Assets.Resources.Culture);
                return !string.IsNullOrEmpty(localized) ? localized : s;
            }
        }
        catch { }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

