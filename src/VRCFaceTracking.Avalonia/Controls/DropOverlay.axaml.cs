using Avalonia;
using Avalonia.Controls;

namespace VRCFaceTracking.Avalonia.Controls;

public partial class DropOverlay : UserControl
{
    public DropOverlay()
    {
        InitializeComponent(true);
        IsOverlayVisible = true;
    }

    public static readonly StyledProperty<bool> IsOverlayVisibleProperty =
        AvaloniaProperty.Register<DropOverlay, bool>(nameof(IsOverlayVisible), false);

    public bool IsOverlayVisible
    {
        get => GetValue(IsOverlayVisibleProperty);
        set => SetValue(IsOverlayVisibleProperty, value);
    }
}
