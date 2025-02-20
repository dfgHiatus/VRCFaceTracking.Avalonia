using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VRCFaceTracking.Avalonia.Controls;

public partial class SettingsExpander : UserControl
{
    public static readonly StyledProperty<string> IconPathProperty =
        AvaloniaProperty.Register<SettingsExpander, string>(nameof(IconPath));

    public string IconPath
    {
        get => GetValue(IconPathProperty);
        set => SetValue(IconPathProperty, value);
    }

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SettingsExpander, string>(nameof(Title));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<SettingsExpander, string>(nameof(Description));

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly StyledProperty<object> ExpanderContentProperty =
            AvaloniaProperty.Register<SettingsExpander, object>(nameof(ExpanderContent));

    public object ExpanderContent
    {
        get => GetValue(ExpanderContentProperty);
        set => SetValue(ExpanderContentProperty, value);
    }

    public SettingsExpander()
    {
        InitializeComponent(true);
        DataContext = this;
    }
}
