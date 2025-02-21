﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Metadata;

namespace VRCFaceTracking.Avalonia.Controls;

public class ContentExample : TemplatedControl
{
    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<Panel>();

    [Content]
    public object? Content {
        get => GetValue(ContentProperty);
        set {
            if (GetValue(ContentProperty) is ILogical oldLogical) LogicalChildren.Remove(oldLogical);
            SetValue(ContentProperty, value);
            if (value is ILogical newLogical) LogicalChildren.Add(newLogical);
        }
    }

    public static readonly StyledProperty<Thickness> PaddingProperty =
            AvaloniaProperty.Register<ContentExample, Thickness>(nameof(Padding));

    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }
}
