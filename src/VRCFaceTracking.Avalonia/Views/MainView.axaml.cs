using Avalonia;
using Avalonia.Controls;
using System;
using VRCFaceTracking.Avalonia.Controls;
using VRCFaceTracking.Avalonia.ViewModels;


namespace VRCFaceTracking.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        SetupViewContainer();

        this.DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void SetupViewContainer()
    {
        if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid()) // mobile
        {
            ApplicationSidePanelLogo.IsVisible = true;
            ViewContainer.CornerRadius = new CornerRadius(0, 0, 0, 0);
        }
        else
        {
            ApplicationSidePanelLogo.IsVisible = false;

            // We will just default to the existing xaml to make it more straight-forward to adjust later.
            //ViewContainer.CornerRadius = new CornerRadius(16, 0, 0, 0);
        }
    }

    private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        var viewModel = DataContext as MainViewModel;
        viewModel.DetachedFromVisualTree();
    }
}
