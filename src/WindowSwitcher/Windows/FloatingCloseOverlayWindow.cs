using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

namespace WindowSwitcher.Windows;

/// <summary>
/// Fully opaque refresh and close buttons hosted in their own top-level window so
/// floating-window opacity changes do not fade them.
/// </summary>
internal sealed class FloatingCloseOverlayWindow : Window
{
    private const double ButtonSize = 20d;
    private const double ButtonGap = 2d;
    private const double EdgeMargin = 3d;

    /// <summary>
    /// Width kept clear at the top-right of the preview for the refresh and close buttons.
    /// </summary>
    internal const double ReservedTopRightWidth = (ButtonSize * 2) + ButtonGap + EdgeMargin;

    private readonly IFloatingWindowHandleConfigurator _handleConfigurator;
    private readonly Button _refreshButton;
    private bool _handleConfigured;

    public event EventHandler? CloseRequested;
    public event EventHandler? RefreshRequested;
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

        _refreshButton = CreateChromeButton(
            CreateRefreshIcon(),
            background: Color.FromRgb(0x37, 0x47, 0x4F),
            border: Color.FromRgb(0x26, 0x32, 0x38)
        );
        _refreshButton.IsVisible = false;
        ToolTip.SetTip(_refreshButton, "Refresh thumbnail");
        _refreshButton.Click += OnRefreshButtonClick;
        _refreshButton.PointerPressed += OnCloseButtonPointerPressed;
        _refreshButton.PointerEntered += (_, _) =>
            OverlayPointerEntered?.Invoke(this, EventArgs.Empty);
        _refreshButton.PointerExited += (_, _) =>
            OverlayPointerExited?.Invoke(this, EventArgs.Empty);

        var closeButton = CreateChromeButton(
            "×",
            background: Color.FromRgb(0xE5, 0x39, 0x35),
            border: Color.FromRgb(0xB7, 0x1C, 0x1C)
        );
        closeButton.FontSize = 13;
        closeButton.FontWeight = FontWeight.Bold;
        ToolTip.SetTip(closeButton, "Close preview");
        closeButton.Click += OnCloseButtonClick;
        closeButton.PointerPressed += OnCloseButtonPointerPressed;
        closeButton.PointerEntered += (_, _) =>
            OverlayPointerEntered?.Invoke(this, EventArgs.Empty);
        closeButton.PointerExited += (_, _) => OverlayPointerExited?.Invoke(this, EventArgs.Empty);

        Content = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = ButtonGap,
            Background = Brushes.Transparent,
            Children = { _refreshButton, closeButton },
        };
        IsVisible = false;
    }

    /// <summary>
    /// Shows the refresh button only while this preview is frozen.
    /// </summary>
    public void SetRefreshVisible(bool visible)
    {
        if (_refreshButton.IsVisible == visible)
            return;

        _refreshButton.IsVisible = visible;
        Width = visible ? (ButtonSize * 2) + ButtonGap : ButtonSize;
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
        double overlayWidth = Width > 0 ? Width : ButtonSize;
        int offsetX = (int)Math.Round((owner.Width - overlayWidth - EdgeMargin) * scale);
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

    private void OnRefreshButtonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static Button CreateChromeButton(object content, Color background, Color border)
    {
        return new Button
        {
            Content = content,
            Width = ButtonSize,
            Height = ButtonSize,
            Padding = new Thickness(0),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
        };
    }

    private static Path CreateRefreshIcon()
    {
        return new Path
        {
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            Fill = Brushes.White,
            Data = Geometry.Parse(
                "M12,5 V1 L7,6 L12,11 V7 C15.31,7 18,9.69 18,13 C18,16.31 15.31,19 12,19 C8.69,19 6,16.31 6,13 H4 C4,17.42 7.58,21 12,21 C16.42,21 20,17.42 20,13 C20,8.58 16.42,5 12,5 Z"
            ),
        };
    }
}
