using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

namespace WindowSwitcher.Windows;

/// <summary>
/// Fully opaque close button hosted in its own top-level window so floating-window
/// opacity changes do not fade it.
/// </summary>
internal sealed class FloatingCloseOverlayWindow : Window
{
    private const double ButtonSize = 20d;
    private const double EdgeMargin = 3d;
    private readonly IFloatingWindowHandleConfigurator _handleConfigurator;
    private bool _handleConfigured;

    public event EventHandler? CloseRequested;
    public event EventHandler? OverlayPointerEntered;
    public event EventHandler? OverlayPointerExited;

    public FloatingCloseOverlayWindow(IFloatingWindowHandleConfigurator handleConfigurator)
    {
        ArgumentNullException.ThrowIfNull(handleConfigurator);
        _handleConfigurator = handleConfigurator;

        Width = ButtonSize;
        Height = ButtonSize;
        CanResize = false;
        CanMinimize = false;
        CanMaximize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        var closeButton = new Button
        {
            Content = "×",
            Width = ButtonSize,
            Height = ButtonSize,
            Padding = new Thickness(0),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xB7, 0x1C, 0x1C)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
        };
        ToolTip.SetTip(closeButton, "Close preview");
        closeButton.Click += OnCloseButtonClick;
        closeButton.PointerPressed += OnCloseButtonPointerPressed;
        closeButton.PointerEntered += (_, _) =>
            OverlayPointerEntered?.Invoke(this, EventArgs.Empty);
        closeButton.PointerExited += (_, _) => OverlayPointerExited?.Invoke(this, EventArgs.Empty);

        Content = closeButton;
        IsVisible = false;
    }

    public void ShowAbove(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!IsVisible)
        {
            Show(owner);
            IsVisible = true;
            ConfigureHandleOnce();
        }

        RepositionAbove(owner);
    }

    public void HideOverlay()
    {
        IsVisible = false;
        Hide();
    }

    public void RepositionAbove(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        double scale = owner.RenderScaling > 0 ? owner.RenderScaling : 1;
        int offsetX = (int)Math.Round((owner.Width - ButtonSize - EdgeMargin) * scale);
        int offsetY = (int)Math.Round(EdgeMargin * scale);
        Position = new PixelPoint(owner.Position.X + offsetX, owner.Position.Y + offsetY);
    }

    private void ConfigureHandleOnce()
    {
        if (_handleConfigured)
            return;

        var platformHandle = TryGetPlatformHandle();
        if (platformHandle is null)
            return;

        _handleConfigurator.Configure(platformHandle.Handle);
        _handleConfigured = true;
    }

    private void OnCloseButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
