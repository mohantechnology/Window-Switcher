using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Serilog;
using WindowSwitcher.Controls;
using WindowSwitcher.Hosting;
using WindowSwitcher.Lib.Data;
using WindowSwitcher.Lib.Data.Platform;
using WindowSwitcher.Lib.Data.Platform.Keybinds.Abstractions;
using WindowSwitcher.Lib.Data.Platform.Keybinds.Models;
using WindowSwitcher.Lib.Data.Platform.Keybinds.Utilities;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;
using WindowSwitcher.Lib.Data.Platform.WindowAccess.Accessors.Abstractions;
using WindowSwitcher.Lib.Data.Platform.WindowAccess.PreviewFrames.Abstractions;
using WindowSwitcher.Lib.Models;
using WindowSwitcher.Windows.Abstractions;
using WindowSwitcher.Windows.Services;

namespace WindowSwitcher.Windows;

public partial class FloatingWindow : Window, IFloatingPreviewWindow
{
    private const double ResizeGripThickness = 8d;
    private const int AttentionPulseStepMilliseconds = 160;
    private const int AttentionPulseStepCount = 6;
    private static readonly ILogger Logger = Log.ForContext<FloatingWindow>();
    private static readonly Cursor DefaultCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor TopSideCursor = new(StandardCursorType.TopSide);
    private static readonly Cursor BottomSideCursor = new(StandardCursorType.BottomSide);
    private static readonly Cursor LeftSideCursor = new(StandardCursorType.LeftSide);
    private static readonly Cursor RightSideCursor = new(StandardCursorType.RightSide);
    private static readonly Cursor TopLeftCornerCursor = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor TopRightCornerCursor = new(StandardCursorType.TopRightCorner);
    private static readonly Cursor BottomLeftCornerCursor = new(
        StandardCursorType.BottomLeftCorner
    );
    private static readonly Cursor BottomRightCornerCursor = new(
        StandardCursorType.BottomRightCorner
    );
    private const string PendingSelectionBorderBrushKey = "PendingSelectionBorderBrush";
    private static readonly IBrush DefaultPendingSelectionBorderBrush = new SolidColorBrush(
        Color.FromRgb(255, 185, 0)
    );
    private readonly IBrush _pendingSelectionBorderBrush;
    private readonly FloatingCloseOverlayWindow _closeOverlay;
    private volatile bool _isPointerInside;
    private bool _leftGestureActive;
    private long _leftGestureStartedAtMs;
    private int _leftGestureStartX;
    private int _leftGestureStartY;
    private bool _leftGestureResizeStarted;
    private bool _leftGestureMoved;
    private bool _closeRequestedByHost;
    private bool _isPreviewSelected;
    private bool _isSelectionPending;
    private bool _isFloatingContentHovered;
    private bool _isCloseOverlayHovered;
    private DispatcherTimer? _closeOverlayHideTimer;
    private DispatcherTimer? _attentionPulseTimer;
    private int _attentionPulseStepsRemaining;
    private readonly IFloatingWindowHost _floatingWindowHost;
    private readonly WinAccessorBase _winAccessorBase;
    private readonly IFloatingPreviewPolicy _floatingPreviewPolicy;
    private readonly IFloatingWindowSettingsService _floatingWindowSettingsService;
    private readonly IWindowKeybindManager _windowKeybindManager;
    private readonly IMetaKeyStateReader _metaKeyStateReader;
    private readonly IFloatingWindowHandleConfigurator _floatingWindowHandleConfigurator;
    private readonly IWindowFrameSizeReader _windowFrameSizeReader;
    private double _sourceAspectRatio;
    private WindowEdge? _activeResizeEdge;
    private int _resizeStartWidth;
    private int _resizeStartHeight;
    private bool _applyingLinkedSize;
    private int _labelFontSize = FloatingWindowLabelLayout.DefaultFontSize;
    private MenuItem? _livePreviewMenuItem;
    private MenuItem? _refreshThumbnailMenuItem;
    private readonly FloatingWindowService _service;

    private int PreviewTitleReservedHeight =>
        FloatingWindowLabelLayout.TitleBandHeight(_labelFontSize);
    public WindowConfig WindowConfig { get; private set; }

    internal FloatingWindow(
        WindowConfig windowConfig,
        WinAccessorBase winAccessorBase,
        IPreviewFrameProvider previewFrameProvider,
        IFloatingWindowHost floatingWindowHost,
        IFloatingWindowSettingsService floatingWindowSettingsService
    )
    {
        ArgumentNullException.ThrowIfNull(windowConfig);
        ArgumentNullException.ThrowIfNull(winAccessorBase);
        ArgumentNullException.ThrowIfNull(previewFrameProvider);
        ArgumentNullException.ThrowIfNull(floatingWindowHost);
        ArgumentNullException.ThrowIfNull(floatingWindowSettingsService);

        InitializeComponent();

        _pendingSelectionBorderBrush = ResolvePendingSelectionBorderBrush();

        WindowConfig = windowConfig;
        _winAccessorBase = winAccessorBase;
        _floatingWindowHost = floatingWindowHost;
        _floatingWindowSettingsService = floatingWindowSettingsService;
        _floatingPreviewPolicy = AppServiceProvider.GetRequiredService<IFloatingPreviewPolicy>();
        _windowKeybindManager = AppServiceProvider.GetRequiredService<IWindowKeybindManager>();
        _metaKeyStateReader = AppServiceProvider.GetRequiredService<IMetaKeyStateReader>();
        var nativeThumbnailRenderer =
            AppServiceProvider.GetRequiredService<INativeThumbnailRenderer>();
        var windowStillImageCapture =
            AppServiceProvider.GetRequiredService<IWindowStillImageCapture>();
        _floatingWindowHandleConfigurator =
            AppServiceProvider.GetRequiredService<IFloatingWindowHandleConfigurator>();
        _windowFrameSizeReader = AppServiceProvider.GetRequiredService<IWindowFrameSizeReader>();

        _closeOverlay = new FloatingCloseOverlayWindow(_floatingWindowHandleConfigurator);
        _closeOverlay.CloseRequested += (_, _) =>
            _floatingWindowHost.CloseFloatingPreview(WindowConfig.WindowId);
        _closeOverlay.OverlayPointerEntered += (_, _) =>
        {
            _isCloseOverlayHovered = true;
            CancelCloseOverlayHide();
            UpdateCloseOverlayVisibility();
        };
        _closeOverlay.OverlayPointerExited += (_, _) =>
        {
            _isCloseOverlayHovered = false;
            ScheduleCloseOverlayHide();
        };

        SetInitialWindowSettings();

        _service = new FloatingWindowService(
            this,
            WindowConfig,
            previewFrameProvider,
            _floatingPreviewPolicy,
            nativeThumbnailRenderer,
            windowStillImageCapture,
            WindowScreenshot,
            DmaBufPreview,
            PreviewBorder
        );
        _service.SetLabelFontSize(_labelFontSize);
        FloatingWindowContextMenu.Opening += (_, _) => UpdatePreviewMenuItems();

        Show();
        _service.UpdateLayout();
        _service.Start();

        var platformHandle = TryGetPlatformHandle();
        if (platformHandle is not null)
        {
            _floatingWindowHandleConfigurator.Configure(platformHandle.Handle);
            // Handle exists only after Show; re-apply so Win32 layered alpha takes effect.
            ApplyPreviewOpacity(syncServiceOpacity: true);
        }
    }

    public sealed override void Show()
    {
        base.Show();
    }

    private void SetInitialWindowSettings()
    {
        _labelFontSize = FloatingWindowLabelLayout.ClampFontSize(
            _floatingWindowSettingsService.GetAppearanceSettings().FloatingWindowLabelFontSize
        );
        WindowConfig? settingsConfig = _floatingWindowSettingsService.GetPersistedConfig(
            WindowConfig
        );
        bool hasPersistedLayout = false;
        if (settingsConfig is not null)
        {
            hasPersistedLayout = true;
            WindowConfig = settingsConfig;

            // Position only for existing window configurations, avoid the window to pop outside the viewport on Linux
            Position = new PixelPoint(WindowConfig.WindowLeft, WindowConfig.WindowTop);
        }

        _sourceAspectRatio = ReadSourceAspect();
        if (!hasPersistedLayout)
        {
            FloatingWindowAppearanceSettings appearance =
                _floatingWindowSettingsService.GetAppearanceSettings();
            WindowConfig.PreviewOpacity = appearance.DefaultPreviewOpacity;
            (int width, int height) = FloatingWindowInitialSize.Resolve(
                hasPersistedLayout: false,
                persistedWidth: WindowConfig.WindowWidth,
                persistedHeight: WindowConfig.WindowHeight,
                useDefaultWindowSize: appearance.UseDefaultWindowSize,
                useFixedWindowSize: appearance.UseFixedWindowSize,
                configuredWidth: appearance.WindowWidth,
                configuredHeight: appearance.WindowHeight
            );
            if (appearance.LinkPreviewAspectRatio && _sourceAspectRatio > 0)
            {
                (width, height) = LinkedPreviewSize.FitWidth(
                    width,
                    _sourceAspectRatio,
                    PreviewTitleReservedHeight
                );
            }

            WindowConfig.WindowWidth = width;
            WindowConfig.WindowHeight = height;
        }

        WindowLabel.Content = WindowConfig.ShortWindowTitle;

        // Avalonia draws its own shadow and frame around BorderOnly windows on X11/XWayland,
        // which shows up as a black margin around the preview; resizing is handled by the canvas.
        if (!_floatingPreviewPolicy.UseWindowBorder)
            WindowDecorations = WindowDecorations.None;

        WindowScreenshot.IsVisible = _floatingPreviewPolicy.ShowScreenshotControl;
        DmaBufPreview.IsVisible = _floatingPreviewPolicy.ShowScreenshotControl;
        FloatingWindowContextMenu.Items.Add(
            new MenuItem()
            {
                Header = "Add to blacklist",
                Command = new ContextMenuCommand(() =>
                    _floatingWindowHost.AddToBlacklist(WindowConfig.WindowTitle)
                ),
            }
        );
        FloatingWindowContextMenu.Items.Add(
            new MenuItem()
            {
                Header = "Add to temp blacklist",
                Command = new ContextMenuCommand(() =>
                    _floatingWindowHost.AddToTempBlacklist(WindowConfig.WindowId)
                ),
            }
        );
        FloatingWindowContextMenu.Items.Add(
            new MenuItem()
            {
                Header = "Rename window",
                Command = new ContextMenuCommand(() => _ = RenameWindowTitleAsync()),
            }
        );
        _livePreviewMenuItem = new MenuItem
        {
            Header = "Disable live preview",
            Command = new ContextMenuCommand(ToggleLivePreview),
        };
        _refreshThumbnailMenuItem = new MenuItem
        {
            Header = "Refresh thumbnail",
            Command = new ContextMenuCommand(RefreshThumbnail),
        };
        FloatingWindowContextMenu.Items.Add(_livePreviewMenuItem);
        FloatingWindowContextMenu.Items.Add(_refreshThumbnailMenuItem);
        if (_floatingWindowHost.CanResetPreviewSelection)
        {
            FloatingWindowContextMenu.Items.Add(new Separator());
            FloatingWindowContextMenu.Items.Add(
                new MenuItem()
                {
                    Header = "Reset preview stream",
                    Command = new ContextMenuCommand(() =>
                        _floatingWindowHost.ResetPreviewSelection(WindowConfig.WindowId)
                    ),
                }
            );
        }

        // Service is constructed after this method; skip service opacity sync until then.
        ApplySettingsCore(refreshPreviewPipeline: false, syncServiceOpacity: false);
    }

    private void CanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _floatingWindowHost.SetActivePreview(this);

        PointerPoint pointerPoint = e.GetCurrentPoint(this);
        if (pointerPoint.Properties.IsLeftButtonPressed)
            BeginLeftClickGesture();
        else
            _leftGestureActive = false;

        if (TryBeginResizeDrag(e))
            return;

        if (_floatingWindowSettingsService.GetBehaviorSettings().MoveWindows)
            BeginMoveDrag(e);
    }

    private void BeginLeftClickGesture()
    {
        _leftGestureActive = true;
        _leftGestureStartedAtMs = Environment.TickCount64;
        _leftGestureStartX = Position.X;
        _leftGestureStartY = Position.Y;
        _leftGestureResizeStarted = false;
        _leftGestureMoved = false;
    }

    private bool TryBeginResizeDrag(PointerPressedEventArgs e)
    {
        if (!CanResize)
            return false;

        PointerPoint pointerPoint = e.GetCurrentPoint(this);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
            return false;

        WindowEdge? resizeEdge = ResolveResizeEdge(pointerPoint.Position);
        if (!resizeEdge.HasValue)
            return false;

        _leftGestureResizeStarted = true;
        _resizeStartWidth = (int)Math.Round(Width);
        _resizeStartHeight = (int)Math.Round(Height);
        _activeResizeEdge = resizeEdge;
        BeginResizeDrag(resizeEdge.Value, e);
        _activeResizeEdge = null;
        return true;
    }

    private WindowEdge? ResolveResizeEdge(Point pointerPosition)
    {
        if (Width <= 0 || Height <= 0)
            return null;

        bool isNearLeft = pointerPosition.X <= ResizeGripThickness;
        bool isNearRight = pointerPosition.X >= Width - ResizeGripThickness;
        bool isNearTop = pointerPosition.Y <= ResizeGripThickness;
        bool isNearBottom = pointerPosition.Y >= Height - ResizeGripThickness;

        if (isNearTop && isNearLeft)
            return WindowEdge.NorthWest;
        if (isNearTop && isNearRight)
            return WindowEdge.NorthEast;
        if (isNearBottom && isNearLeft)
            return WindowEdge.SouthWest;
        if (isNearBottom && isNearRight)
            return WindowEdge.SouthEast;
        if (isNearTop)
            return WindowEdge.North;
        if (isNearBottom)
            return WindowEdge.South;
        if (isNearLeft)
            return WindowEdge.West;
        if (isNearRight)
            return WindowEdge.East;

        return null;
    }

    private void UpdateResizeCursor(PointerEventArgs e)
    {
        if (!CanResize)
        {
            WindowCanvas.Cursor = DefaultCursor;
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(this);
        WindowEdge? edge = ResolveResizeEdge(pointerPoint.Position);
        WindowCanvas.Cursor = ResolveCursor(edge);
    }

    private static Cursor ResolveCursor(WindowEdge? edge)
    {
        return edge switch
        {
            WindowEdge.North => TopSideCursor,
            WindowEdge.South => BottomSideCursor,
            WindowEdge.West => LeftSideCursor,
            WindowEdge.East => RightSideCursor,
            WindowEdge.NorthWest => TopLeftCornerCursor,
            WindowEdge.NorthEast => TopRightCornerCursor,
            WindowEdge.SouthWest => BottomLeftCornerCursor,
            WindowEdge.SouthEast => BottomRightCornerCursor,
            _ => DefaultCursor,
        };
    }

    private void CanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !_leftGestureActive)
        {
            _leftGestureActive = false;
            return;
        }

        long holdDurationMs = Environment.TickCount64 - _leftGestureStartedAtMs;
        bool activate = FloatingPreviewClickGesture.IsActivationClick(
            holdDurationMs,
            _leftGestureStartX,
            _leftGestureStartY,
            Position.X,
            Position.Y,
            _leftGestureResizeStarted,
            _leftGestureMoved
        );
        _leftGestureActive = false;

        if (!activate)
            return;

        _ = RaiseWindowAndNotifyActivationAsync();
    }

    private void FloatingRootPointerEntered(object? sender, PointerEventArgs e)
    {
        _isFloatingContentHovered = true;
        CancelCloseOverlayHide();
        UpdateCloseOverlayVisibility();
    }

    private void FloatingRootPointerExited(object? sender, PointerEventArgs e)
    {
        _isFloatingContentHovered = false;
        ScheduleCloseOverlayHide();
    }

    private void FloatingWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (
            _leftGestureActive
            && FloatingPreviewClickGesture.ExceedsDragThreshold(
                _leftGestureStartX,
                _leftGestureStartY,
                Position.X,
                Position.Y
            )
        )
        {
            _leftGestureMoved = true;
        }

        if (_closeOverlay.IsVisible)
            _closeOverlay.RepositionAbove(this);
    }

    private void CanvasPointerEntered(object? sender, PointerEventArgs e)
    {
        UpdateResizeCursor(e);

        if (_isPointerInside)
            return;
        _isPointerInside = true;

        if (!_floatingWindowSettingsService.GetBehaviorSettings().FocusOnHover)
            return;

        PointerPoint point = e.GetCurrentPoint(this);
        if (
            point.Properties.IsLeftButtonPressed
            || point.Properties.IsRightButtonPressed
            || point.Properties.IsMiddleButtonPressed
        )
            return;

        _floatingWindowHost.SetActivePreview(this);
        _ = RaiseWindowAndNotifyActivationAsync();
    }

    private async Task RaiseWindowAndNotifyActivationAsync()
    {
        try
        {
            bool activated = await _winAccessorBase.TryActivateWindowAsync(WindowConfig.WindowId);
            if (!activated)
                return;
            _floatingWindowHost.NotifyPreviewWindowActivated(WindowConfig.WindowId);
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Activating window {WindowId} from its preview failed",
                WindowConfig.WindowId
            );
        }
    }

    private void CanvasPointerExited(object? sender, PointerEventArgs e)
    {
        _isPointerInside = false;
        WindowCanvas.Cursor = DefaultCursor;
    }

    private void FloatingWindowPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (Math.Abs(e.Delta.Y) < double.Epsilon)
            return;

        bool increase = e.Delta.Y > 0;
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        // Win is often missing from Avalonia pointer modifiers on Windows; merge OS state.
        bool meta =
            e.KeyModifiers.HasFlag(KeyModifiers.Meta) || _metaKeyStateReader.IsMetaPressed();

        FloatingWindowBehaviorSettings behavior =
            _floatingWindowSettingsService.GetBehaviorSettings();
        FloatingWindowAppearanceSettings appearance =
            _floatingWindowSettingsService.GetAppearanceSettings();

        if (
            behavior.ChangeSizeOnHover
            && !appearance.UseFixedWindowSize
            && MatchesConfiguredMouseWheelShortcut(
                KeybindBuiltInTargets.ChangeFloatingSizeOnHoverTargetId,
                ctrl,
                alt,
                shift,
                meta
            )
        )
        {
            ApplyHoverSizeChange(increase);
            e.Handled = true;
            return;
        }

        if (
            behavior.ChangeOpacityOnHover
            && MatchesConfiguredMouseWheelShortcut(
                KeybindBuiltInTargets.ChangeFloatingOpacityOnHoverTargetId,
                ctrl,
                alt,
                shift,
                meta
            )
        )
        {
            ApplyHoverOpacityChange(increase);
            e.Handled = true;
        }
    }

    private bool MatchesConfiguredMouseWheelShortcut(
        string targetId,
        bool ctrl,
        bool alt,
        bool shift,
        bool meta
    )
    {
        IReadOnlyCollection<WindowKeybindShortcut> shortcuts =
            _windowKeybindManager.GetShortcutsForTarget(targetId);
        return shortcuts.Any(shortcut =>
            FloatingPreviewHoverAdjustment.MatchesMouseWheelShortcut(
                shortcut.Combination,
                ctrl,
                alt,
                shift,
                meta
            )
        );
    }

    private void ApplyHoverSizeChange(bool increase)
    {
        int currentWidth = (int)Math.Max(1, Width);
        int currentHeight = (int)Math.Max(1, Height);
        (int nextWidth, int nextHeight) = LinksPreviewAspect()
            ? LinkedPreviewSize.Scale(
                currentWidth,
                currentHeight,
                increase,
                CurrentAspectRatio(currentWidth, currentHeight),
                PreviewTitleReservedHeight
            )
            : FloatingPreviewHoverAdjustment.ScaleSize(currentWidth, currentHeight, increase);

        if (nextWidth == (int)Width && nextHeight == (int)Height)
            return;

        Width = nextWidth;
        Height = nextHeight;
        WindowConfig.WindowWidth = nextWidth;
        WindowConfig.WindowHeight = nextHeight;
        _floatingWindowSettingsService.Save(WindowConfig);
        _service.OnWindowResized();
    }

    private void ApplyHoverOpacityChange(bool increase)
    {
        double nextOpacity = FloatingPreviewHoverAdjustment.AdjustOpacity(
            WindowConfig.PreviewOpacity,
            increase
        );
        if (Math.Abs(nextOpacity - WindowConfig.PreviewOpacity) < double.Epsilon)
            return;

        WindowConfig.PreviewOpacity = nextOpacity;
        ApplyPreviewOpacity();
        _floatingWindowSettingsService.Save(WindowConfig);
    }

    private void ApplyPreviewOpacity(bool syncServiceOpacity = true)
    {
        double opacity = Math.Clamp(
            WindowConfig.PreviewOpacity,
            FloatingPreviewHoverAdjustment.MinOpacity,
            FloatingPreviewHoverAdjustment.MaxOpacity
        );
        WindowConfig.PreviewOpacity = opacity;

        // Avalonia Opacity helps on platforms where the compositor honors it; on Windows we also
        // set WS_EX_LAYERED alpha so the whole HWND (including DWM thumbnails) shows apps beneath.
        Opacity = opacity;
        var platformHandle = TryGetPlatformHandle();
        if (platformHandle is not null)
            _floatingWindowHandleConfigurator.SetOpacity(platformHandle.Handle, opacity);

        // Keep native thumbnail fully opaque inside this window; window alpha handles fade.
        // Constructor applies settings before FloatingWindowService exists.
        if (syncServiceOpacity)
            _service.SetPreviewOpacity(FloatingPreviewHoverAdjustment.MaxOpacity);
    }

    private void UpdateCloseOverlayVisibility()
    {
        if (_closeRequestedByHost)
        {
            _closeOverlay.HideOverlay();
            return;
        }

        if (_isFloatingContentHovered || _isCloseOverlayHovered)
            _closeOverlay.ShowAbove(this);
        else
            _closeOverlay.HideOverlay();
    }

    private void ScheduleCloseOverlayHide()
    {
        CancelCloseOverlayHide();
        _closeOverlayHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _closeOverlayHideTimer.Tick += OnCloseOverlayHideTimerTick;
        _closeOverlayHideTimer.Start();
    }

    private void OnCloseOverlayHideTimerTick(object? sender, EventArgs e)
    {
        CancelCloseOverlayHide();
        UpdateCloseOverlayVisibility();
    }

    private void CancelCloseOverlayHide()
    {
        if (_closeOverlayHideTimer is null)
            return;

        _closeOverlayHideTimer.Stop();
        _closeOverlayHideTimer.Tick -= OnCloseOverlayHideTimerTick;
        _closeOverlayHideTimer = null;
    }

    private void CanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        UpdateResizeCursor(e);
    }

    private double ReadSourceAspect()
    {
        if (
            !_windowFrameSizeReader.TryGetFrameSize(
                WindowConfig.WindowId,
                out int width,
                out int height
            )
        )
            return 0;

        return LinkedPreviewSize.TryAspect(width, height, out double aspect) ? aspect : 0;
    }

    private bool LinksPreviewAspect() =>
        _floatingWindowSettingsService.GetAppearanceSettings().LinkPreviewAspectRatio;

    private double CurrentAspectRatio(int width, int height)
    {
        if (_sourceAspectRatio > 0)
            return _sourceAspectRatio;

        int contentHeight = Math.Max(1, height - PreviewTitleReservedHeight);
        return LinkedPreviewSize.TryAspect(width, contentHeight, out double aspect) ? aspect : 1;
    }

    private bool WidthDrivesResize(WindowEdge edge)
    {
        bool horizontal = edge is WindowEdge.East or WindowEdge.West;
        bool vertical = edge is WindowEdge.North or WindowEdge.South;
        if (horizontal && !vertical)
            return true;
        if (vertical && !horizontal)
            return false;

        int widthDelta = Math.Abs((int)Math.Round(Width) - _resizeStartWidth);
        int heightDelta = Math.Abs((int)Math.Round(Height) - _resizeStartHeight);
        return widthDelta >= heightDelta;
    }

    private void FloatingWindowResized(object? sender, WindowResizedEventArgs e)
    {
        if (!_applyingLinkedSize && _activeResizeEdge is WindowEdge edge && LinksPreviewAspect())
        {
            bool widthDrives = WidthDrivesResize(edge);
            (int linkedWidth, int linkedHeight) = LinkedPreviewSize.Preserve(
                (int)Math.Round(Width),
                (int)Math.Round(Height),
                CurrentAspectRatio(_resizeStartWidth, _resizeStartHeight),
                widthDrives,
                PreviewTitleReservedHeight
            );
            if (
                linkedWidth != (int)Math.Round(Width)
                || linkedHeight != (int)Math.Round(Height)
            )
            {
                _applyingLinkedSize = true;
                Width = linkedWidth;
                Height = linkedHeight;
                _applyingLinkedSize = false;
            }
        }

        WindowConfig.WindowHeight = (Int32)Height;
        WindowConfig.WindowWidth = (Int32)Width;
        _service.OnWindowResized();
        if (_closeOverlay.IsVisible)
            _closeOverlay.RepositionAbove(this);
    }

    private void WindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        WindowConfig.WindowLeft = Position.X;
        WindowConfig.WindowTop = Position.Y;
        _service.OnPointerReleased();
    }

    private void FloatingWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        _floatingWindowSettingsService.Save(WindowConfig);
        _floatingWindowHost.ClearActivePreview(this);
        bool allowClose = StaticData.AppClosing || _closeRequestedByHost;
        e.Cancel = !allowClose;
        if (!e.Cancel)
        {
            StopAttentionPulse(restoreHighlight: false);
            CancelCloseOverlayHide();
            _closeOverlay.HideOverlay();
            _closeOverlay.Close();
            _service.Stop(WindowConfig.WindowId);
        }
    }

    private async Task RenameWindowTitleAsync()
    {
        await _floatingWindowHost.RenameWindowTitleAsync(WindowConfig.WindowId);
    }

    public void SetPreviewHighlight(bool isSelected)
    {
        _isPreviewSelected = isSelected;
        if (!_isSelectionPending && _attentionPulseTimer is null)
            _service.SetPreviewHighlight(isSelected);
    }

    /// <summary>
    /// Blinks the preview border so a pin of an already-open window is visible.
    /// </summary>
    public void PulseAlreadyOpen()
    {
        StopAttentionPulse(restoreHighlight: false);
        _attentionPulseStepsRemaining = AttentionPulseStepCount;
        _service.SetPreviewHighlight(true);
        _attentionPulseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AttentionPulseStepMilliseconds),
        };
        _attentionPulseTimer.Tick += OnAttentionPulseTick;
        _attentionPulseTimer.Start();
    }

    private void OnAttentionPulseTick(object? sender, EventArgs e)
    {
        _attentionPulseStepsRemaining--;
        if (_attentionPulseStepsRemaining <= 0)
        {
            StopAttentionPulse(restoreHighlight: true);
            return;
        }

        _service.SetPreviewHighlight(_attentionPulseStepsRemaining % 2 == 0);
    }

    private void StopAttentionPulse(bool restoreHighlight)
    {
        if (_attentionPulseTimer is not null)
        {
            _attentionPulseTimer.Stop();
            _attentionPulseTimer.Tick -= OnAttentionPulseTick;
            _attentionPulseTimer = null;
        }

        _attentionPulseStepsRemaining = 0;
        if (restoreHighlight)
            _service.SetPreviewHighlight(_isSelectionPending || _isPreviewSelected);
    }

    internal void SetSelectionPending(bool isPending)
    {
        _isSelectionPending = isPending;
        ApplyPreviewBorderAppearance();
        if (_attentionPulseTimer is null)
            _service.SetPreviewHighlight(isPending || _isPreviewSelected);
    }

    public void UpdateWindowTitle(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            return;

        WindowConfig.WindowTitle = newTitle;
        WindowLabel.Content = WindowConfig.ShortWindowTitle;
    }

    public void ApplySettings()
    {
        SyncLivePreviewOverride();
        ApplySettingsCore(refreshPreviewPipeline: true);
    }

    private void SyncLivePreviewOverride()
    {
        WindowConfig? persisted = _floatingWindowSettingsService.GetPersistedConfig(WindowConfig);
        WindowConfig.LivePreviewOverride = persisted?.LivePreviewOverride;
    }

    private void UpdatePreviewMenuItems()
    {
        bool livePreviewEnabled = _service.IsLivePreviewEnabled;
        if (_livePreviewMenuItem is not null)
        {
            _livePreviewMenuItem.Header = livePreviewEnabled
                ? "Disable live preview"
                : "Enable live preview";
        }

        if (_refreshThumbnailMenuItem is not null)
            _refreshThumbnailMenuItem.IsEnabled = !livePreviewEnabled;
    }

    private void ToggleLivePreview()
    {
        WindowConfig.LivePreviewOverride = !_service.IsLivePreviewEnabled;
        _floatingWindowSettingsService.Save(WindowConfig);
        _service.ApplySettings();
    }

    private void RefreshThumbnail()
    {
        _service.RefreshFrozenThumbnail();
    }

    public void ApplyDefaultPreviewOpacity(double opacity)
    {
        WindowConfig.PreviewOpacity = Math.Clamp(
            opacity,
            FloatingPreviewHoverAdjustment.MinOpacity,
            FloatingPreviewHoverAdjustment.MaxOpacity
        );
        ApplyPreviewOpacity();
        _floatingWindowSettingsService.Save(WindowConfig);
    }

    public void RequestCloseFromHost()
    {
        if (_closeRequestedByHost)
            return;

        _closeRequestedByHost = true;
        StopAttentionPulse(restoreHighlight: false);
        CancelCloseOverlayHide();
        _closeOverlay.HideOverlay();
        _service.Stop(WindowConfig.WindowId);
        Close();
    }

    private void ApplySettingsCore(bool refreshPreviewPipeline, bool syncServiceOpacity = true)
    {
        FloatingWindowAppearanceSettings configSnapshot =
            _floatingWindowSettingsService.GetAppearanceSettings();
        _labelFontSize = FloatingWindowLabelLayout.ClampFontSize(
            configSnapshot.FloatingWindowLabelFontSize
        );
        WindowLabel.FontSize = _labelFontSize;
        if (syncServiceOpacity || refreshPreviewPipeline)
            _service.SetLabelFontSize(_labelFontSize);

        CanResize = configSnapshot.ResizeWindows;
        if (configSnapshot.UseFixedWindowSize)
        {
            CanResize = false;
            Width = configSnapshot.WindowWidth;
            Height = configSnapshot.WindowHeight;
        }
        else
        {
            Width = WindowConfig.WindowWidth;
            Height = WindowConfig.WindowHeight;
        }

        if (!CanResize)
            WindowCanvas.Cursor = DefaultCursor;

        if (Color.TryParse(configSnapshot.PreviewHighlightColor, out Color highlightColor))
            WindowLabel.Foreground = new SolidColorBrush(highlightColor);

        ApplyPreviewOpacity(syncServiceOpacity);
        ApplyPreviewBorderAppearance();

        if (refreshPreviewPipeline)
            _service.ApplySettings();
    }

    private void ApplyPreviewBorderAppearance()
    {
        PreviewBorder.BorderThickness = new Thickness(_isSelectionPending ? 4 : 2);
        PreviewBorder.BorderBrush = _isSelectionPending
            ? _pendingSelectionBorderBrush
            : WindowLabel.Foreground;
    }

    private static IBrush ResolvePendingSelectionBorderBrush()
    {
        if (
            Application.Current?.TryFindResource(
                PendingSelectionBorderBrushKey,
                out object? resource
            ) == true
            && resource is IBrush brush
        )
        {
            return brush;
        }

        return DefaultPendingSelectionBorderBrush;
    }
}
