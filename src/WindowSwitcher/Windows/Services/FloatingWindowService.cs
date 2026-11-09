using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Serilog;
using WindowSwitcher.Controls;
using WindowSwitcher.Lib.Data;
using WindowSwitcher.Lib.Data.Platform;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;
using WindowSwitcher.Lib.Data.Platform.WindowAccess.PreviewFrames.Abstractions;
using WindowSwitcher.Lib.Models;

namespace WindowSwitcher.Windows.Services;

internal sealed class FloatingWindowService
{
    // Leave room for the hover close button chrome above the native DWM thumbnail.
    private const double TitleReservedHeight = 26;
    private const double PreviewBorderThickness = 2;
    private const int PreviewRequestTimeoutMs = 1_500;
    private const int ResizePreviewRefreshDebounceMs = 250;
    private static readonly TimeSpan GpuInitializationTimeout = TimeSpan.FromSeconds(1);
    private static readonly ILogger Logger = Log.ForContext<FloatingWindowService>();
    private readonly Window _ownerWindow;
    private readonly WindowConfig _windowConfig;
    private readonly IPreviewFrameProvider _previewFrameProvider;
    private readonly IFloatingPreviewPolicy _floatingPreviewPolicy;
    private readonly INativeThumbnailRenderer _nativeThumbnailRenderer;
    private readonly IWindowStillImageCapture _windowStillImageCapture;
    private readonly Image _windowScreenshot;
    private readonly DmaBufPreviewControl _dmaBufPreview;
    private readonly Border _previewBorder;
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _previewOperationCancellationSync = new();
    private readonly SemaphoreSlim _previewUpdateSemaphore = new(1, 1);
    private readonly Lock _pendingFrameSync = new();
    private readonly Lock _resizePreviewRefreshSync = new();
    private CancellationTokenSource? _previewOperationCancellation = new();
    private CancellationTokenSource? _resizePreviewRefreshCancellation;
    private PreviewFrame? _pendingFrame;
    private WriteableBitmap? _streamBitmapA;
    private WriteableBitmap? _streamBitmapB;
    private WriteableBitmap? _frozenPreviewBitmap;
    private PixelSize _streamBitmapSize;
    private nint _thumbnailHandle;
    private int _nextStreamBitmapIndex;
    private int _pendingFrameDrainScheduled;
    private int _previewCaptureEnabled;
    private int _targetScreenshotWidthPx;
    private int _targetScreenshotHeightPx;
    private volatile bool _isClosing;
    private bool _previewCaptureSuspendedWhileDisabled;
    private bool _showingFrozenPreview;
    private bool _frozenSnapshotReady;
    private bool _resizePreviewRefreshPending;

    public FloatingWindowService(
        Window ownerWindow,
        WindowConfig windowConfig,
        IPreviewFrameProvider previewFrameProvider,
        IFloatingPreviewPolicy floatingPreviewPolicy,
        INativeThumbnailRenderer nativeThumbnailRenderer,
        IWindowStillImageCapture windowStillImageCapture,
        Image windowScreenshot,
        DmaBufPreviewControl dmaBufPreview,
        Border previewBorder
    )
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(windowConfig);
        ArgumentNullException.ThrowIfNull(previewFrameProvider);
        ArgumentNullException.ThrowIfNull(floatingPreviewPolicy);
        ArgumentNullException.ThrowIfNull(nativeThumbnailRenderer);
        ArgumentNullException.ThrowIfNull(windowStillImageCapture);
        ArgumentNullException.ThrowIfNull(windowScreenshot);
        ArgumentNullException.ThrowIfNull(dmaBufPreview);
        ArgumentNullException.ThrowIfNull(previewBorder);

        _ownerWindow = ownerWindow;
        _windowConfig = windowConfig;
        _previewFrameProvider = previewFrameProvider;
        _floatingPreviewPolicy = floatingPreviewPolicy;
        _nativeThumbnailRenderer = nativeThumbnailRenderer;
        _windowStillImageCapture = windowStillImageCapture;
        _windowScreenshot = windowScreenshot;
        _dmaBufPreview = dmaBufPreview;
        _dmaBufPreview.ImportFailed += OnDmaBufImportFailed;
        _previewBorder = previewBorder;
        _previewCaptureEnabled = ReadPreviewCaptureEnabled() ? 1 : 0;
        _previewCaptureSuspendedWhileDisabled = !IsPreviewCaptureEnabled();
    }

    public void Start() => _ = Task.Run(() => RunPreviewLoopAsync(_cts.Token));

    public void OnWindowResized()
    {
        UpdateLayout();
        if (!_floatingPreviewPolicy.UseNativeThumbnailPreview)
            ScheduleResizePreviewRefresh();
        else if (IsPreviewCaptureEnabled())
            RegisterWindowThumbnail();
    }

    public void OnPointerReleased() => FlushPendingResizePreviewRefresh();

    public void SetPreviewHighlight(bool isSelected) => _previewBorder.IsVisible = isSelected;

    public void SetPreviewOpacity(double opacity)
    {
        if (_isClosing)
            return;

        // Floating window translucency is applied via native window alpha so apps beneath
        // remain visible. Keep the native thumbnail fully opaque inside that window.
        if (!_floatingPreviewPolicy.UseNativeThumbnailPreview || _thumbnailHandle == 0)
            return;

        byte byteOpacity = FloatingPreviewHoverAdjustment.ToByteOpacity(
            FloatingPreviewHoverAdjustment.MaxOpacity
        );
        _nativeThumbnailRenderer.TryUpdateOpacity(_thumbnailHandle, byteOpacity);
        _ = opacity; // retained for call-site compatibility; window alpha owns translucency
    }

    public void UpdateLayout()
    {
        double topOffset = TitleReservedHeight;
        double previewWidth = RoundToPixel(Math.Max(0, _ownerWindow.Width));
        double previewHeight = RoundToPixel(Math.Max(0, _ownerWindow.Height - topOffset));
        double left = RoundToPixel(0);
        double top = RoundToPixel(topOffset);

        Canvas.SetLeft(_previewBorder, left);
        Canvas.SetTop(_previewBorder, top);
        _previewBorder.Width = previewWidth;
        _previewBorder.Height = previewHeight;

        double scale = _ownerWindow.RenderScaling > 0 ? _ownerWindow.RenderScaling : 1;
        Volatile.Write(ref _targetScreenshotWidthPx, (int)Math.Round(previewWidth * scale));
        Volatile.Write(ref _targetScreenshotHeightPx, (int)Math.Round(previewHeight * scale));

        if (!_floatingPreviewPolicy.ShowScreenshotControl && !_showingFrozenPreview)
            return;

        Canvas.SetLeft(_windowScreenshot, left);
        Canvas.SetTop(_windowScreenshot, top);
        _windowScreenshot.Width = previewWidth;
        _windowScreenshot.Height = previewHeight;
        Canvas.SetLeft(_dmaBufPreview, left);
        Canvas.SetTop(_dmaBufPreview, top);
        _dmaBufPreview.Width = previewWidth;
        _dmaBufPreview.Height = previewHeight;
    }

    public void Stop(string windowId)
    {
        if (_isClosing)
            return;

        _isClosing = true;
        _dmaBufPreview.ImportFailed -= OnDmaBufImportFailed;
        CancelPendingResizePreviewRefresh(executeRefresh: false);
        _cts.Cancel();
        CancelPreviewOperations(recreateTokenSource: false);
        DisposePendingFrame();
        _dmaBufPreview.ClearFrame();
        _previewFrameProvider.ForgetWindow(windowId);
        if (_floatingPreviewPolicy.UseNativeThumbnailPreview)
            UnregisterWindowThumbnail();
        _windowScreenshot.Source = null;
        DisposeFrozenPreviewBitmap();
        DisposeStreamBitmaps();
        _showingFrozenPreview = false;
        _frozenSnapshotReady = false;
    }

    public void ApplySettings()
    {
        if (_isClosing)
            return;

        CancelPendingResizePreviewRefresh(executeRefresh: false);
        UpdateLayout();
        CancelPreviewOperations(recreateTokenSource: true);
        bool enabled = ReadPreviewCaptureEnabled();
        Volatile.Write(ref _previewCaptureEnabled, enabled ? 1 : 0);
        if (!enabled)
        {
            if (!_previewCaptureSuspendedWhileDisabled)
            {
                _previewFrameProvider.SuspendWindow(_windowConfig.WindowId);
                _previewCaptureSuspendedWhileDisabled = true;
            }

            // Keep the last shown frame; on Windows freeze a still capture before
            // unregistering the live DWM thumbnail so the surface does not go blank.
            if (_floatingPreviewPolicy.UseNativeThumbnailPreview)
                FreezeNativeThumbnailPreview();
            else
                _frozenSnapshotReady = true;
            return;
        }

        _previewCaptureSuspendedWhileDisabled = false;
        _frozenSnapshotReady = false;
        ClearFrozenPreview();
        if (_floatingPreviewPolicy.UseNativeThumbnailPreview)
            RegisterWindowThumbnail();
    }

    private async Task RunPreviewLoopAsync(CancellationToken cancellationToken)
    {
        if (_floatingPreviewPolicy.UseNativeThumbnailPreview)
        {
            if (IsPreviewCaptureEnabled())
                RegisterWindowThumbnail();
            else
                FreezeNativeThumbnailPreview();
            return;
        }

        try
        {
            await Task.Delay(Random.Shared.Next(0, 400), cancellationToken).ConfigureAwait(false);
            await ConfigureGpuFrameSupportAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The floating window closed before the preview loop started.
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            using CancellationTokenSource operationCts = CreatePreviewOperationTokenSource(
                cancellationToken
            );
            CancellationToken operationToken = operationCts.Token;
            try
            {
                if (!IsPreviewCaptureEnabled())
                {
                    await EnsureDisabledSnapshotAsync(operationToken).ConfigureAwait(false);
                    await Task.Delay(100, operationToken).ConfigureAwait(false);
                    continue;
                }

                _previewCaptureSuspendedWhileDisabled = false;
                _frozenSnapshotReady = false;

                var request = new ScreenshotRequest(
                    MaxWidthPx: PositiveOrNull(Volatile.Read(ref _targetScreenshotWidthPx)),
                    MaxHeightPx: PositiveOrNull(Volatile.Read(ref _targetScreenshotHeightPx)),
                    TimeoutMs: PreviewRequestTimeoutMs
                );
                await foreach (
                    PreviewFrame frame in _previewFrameProvider.StreamAsync(
                        _windowConfig.WindowId,
                        request,
                        operationToken
                    )
                )
                {
                    if (!IsPreviewCaptureEnabled())
                    {
                        // Keep this last frame as the frozen snapshot when previews are turned off.
                        await ApplyFrameAsync(frame, operationToken, forceApply: true)
                            .ConfigureAwait(false);
                        _frozenSnapshotReady = true;
                        break;
                    }
                    QueueLatestFrame(frame, operationToken);
                }

                await Task.Delay(500, operationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
            {
                // Window closing, settings change or resize restarted the preview operation.
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "Preview frame stream failed for window {WindowId}",
                    _windowConfig.WindowId
                );
                await DelayAfterFailureAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static int? PositiveOrNull(int value) => value > 0 ? value : null;

    private async Task ConfigureGpuFrameSupportAsync(CancellationToken cancellationToken)
    {
        if (_previewFrameProvider is not IPreviewGpuFallback gpuFallback)
            return;
        bool available = await _dmaBufPreview
            .WaitForAvailabilityAsync(GpuInitializationTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (!available)
        {
            _dmaBufPreview.DisableRendering();
            gpuFallback.DisableGpuFrames(_windowConfig.WindowId);
        }
    }

    private void OnDmaBufImportFailed(object? sender, EventArgs eventArgs)
    {
        _dmaBufPreview.DisableRendering();
        if (_previewFrameProvider is IPreviewGpuFallback gpuFallback)
            gpuFallback.DisableGpuFrames(_windowConfig.WindowId);
    }

    private static async Task DelayAfterFailureAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The floating window closed during the retry delay; the loop exits on its own.
        }
    }

    private void QueueLatestFrame(PreviewFrame frame, CancellationToken cancellationToken)
    {
        PreviewFrame? disposeNow;
        bool scheduleDrain = false;
        lock (_pendingFrameSync)
        {
            if (
                _isClosing
                || cancellationToken.IsCancellationRequested
                || !IsPreviewCaptureEnabled()
            )
            {
                disposeNow = frame;
            }
            else
            {
                disposeNow = _pendingFrame;
                _pendingFrame = frame;
                scheduleDrain =
                    Interlocked.CompareExchange(ref _pendingFrameDrainScheduled, 1, 0) == 0;
            }
        }

        disposeNow?.Dispose();
        if (scheduleDrain)
            _ = ProcessQueuedFramesAsync(cancellationToken);
    }

    private async Task ProcessQueuedFramesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                PreviewFrame? frame;
                lock (_pendingFrameSync)
                {
                    frame = _pendingFrame;
                    _pendingFrame = null;
                }
                if (frame is null)
                    break;
                await ApplyFrameAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Preview operation cancelled: pending frames are released in the finally block.
        }
        finally
        {
            Interlocked.Exchange(ref _pendingFrameDrainScheduled, 0);
            bool scheduleDrain = false;
            lock (_pendingFrameSync)
            {
                if (_isClosing || cancellationToken.IsCancellationRequested)
                {
                    _pendingFrame?.Dispose();
                    _pendingFrame = null;
                }
                else if (_pendingFrame is not null)
                {
                    scheduleDrain =
                        Interlocked.CompareExchange(ref _pendingFrameDrainScheduled, 1, 0) == 0;
                }
            }
            if (scheduleDrain)
                _ = ProcessQueuedFramesAsync(cancellationToken);
        }
    }

    private async Task ApplyFrameAsync(
        PreviewFrame frame,
        CancellationToken cancellationToken,
        bool forceApply = false
    )
    {
        bool lockTaken = false;
        bool ownershipTransferred = false;
        try
        {
            await _previewUpdateSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            lockTaken = true;
            if (_isClosing || (!forceApply && !IsPreviewCaptureEnabled()))
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_isClosing || cancellationToken.IsCancellationRequested)
                    return;
                if (frame is LinuxDmaBufPreviewFrame dmaBufFrame)
                {
                    _windowScreenshot.Source = null;
                    DisposeStreamBitmaps();
                    _dmaBufPreview.Present(dmaBufFrame);
                    ownershipTransferred = true;
                    return;
                }
                if (frame is not NativeBgraPreviewFrame cpuFrame)
                    return;
                _dmaBufPreview.ClearFrame();
                WriteableBitmap? bitmap = GetNextStreamBitmap(frame.WidthPx, frame.HeightPx);
                if (bitmap is null)
                    return;
                using ILockedFramebuffer framebuffer = bitmap.Lock();
                if (
                    framebuffer.Address != IntPtr.Zero
                    && cpuFrame.TryCopyTo(framebuffer.Address, framebuffer.RowBytes)
                )
                {
                    _windowScreenshot.Source = bitmap;
                }
            });
        }
        finally
        {
            if (!ownershipTransferred)
                frame.Dispose();
            if (lockTaken)
                _previewUpdateSemaphore.Release();
        }
    }

    private async Task EnsureDisabledSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_frozenSnapshotReady)
        {
            DisposePendingFrame();
            if (!_previewCaptureSuspendedWhileDisabled)
            {
                _previewFrameProvider.SuspendWindow(_windowConfig.WindowId);
                _previewCaptureSuspendedWhileDisabled = true;
            }
            return;
        }

        var request = new ScreenshotRequest(
            MaxWidthPx: PositiveOrNull(Volatile.Read(ref _targetScreenshotWidthPx)),
            MaxHeightPx: PositiveOrNull(Volatile.Read(ref _targetScreenshotHeightPx)),
            TimeoutMs: PreviewRequestTimeoutMs
        );

        try
        {
            await foreach (
                PreviewFrame frame in _previewFrameProvider.StreamAsync(
                    _windowConfig.WindowId,
                    request,
                    cancellationToken
                )
            )
            {
                await ApplyFrameAsync(frame, cancellationToken, forceApply: true)
                    .ConfigureAwait(false);
                _frozenSnapshotReady = true;
                break;
            }
        }
        finally
        {
            _previewFrameProvider.SuspendWindow(_windowConfig.WindowId);
            _previewCaptureSuspendedWhileDisabled = true;
        }
    }

    private void FreezeNativeThumbnailPreview()
    {
        if (_frozenSnapshotReady && _showingFrozenPreview)
        {
            UnregisterWindowThumbnail();
            return;
        }

        if (!long.TryParse(_windowConfig.WindowId, out long sourceHandle))
        {
            UnregisterWindowThumbnail();
            return;
        }

        bool captured = _windowStillImageCapture.TryCapture(
            new IntPtr(sourceHandle),
            PositiveOrNull(Volatile.Read(ref _targetScreenshotWidthPx)),
            PositiveOrNull(Volatile.Read(ref _targetScreenshotHeightPx)),
            out WindowStillImage? stillImage
        );
        UnregisterWindowThumbnail();
        if (!captured || stillImage is null)
            return;

        try
        {
            Dispatcher.UIThread.Invoke(() => ApplyFrozenStillImage(stillImage));
            _frozenSnapshotReady = true;
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Applying frozen preview image failed");
        }
    }

    private void ApplyFrozenStillImage(WindowStillImage stillImage)
    {
        var size = new PixelSize(stillImage.WidthPx, stillImage.HeightPx);
        var bitmap = new WriteableBitmap(
            size,
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque
        );
        using (ILockedFramebuffer framebuffer = bitmap.Lock())
        {
            int destinationStride = framebuffer.RowBytes;
            int sourceStride = stillImage.Stride;
            int rowBytes = stillImage.WidthPx * 4;
            for (int row = 0; row < stillImage.HeightPx; row++)
            {
                Marshal.Copy(
                    stillImage.BgraPixels,
                    row * sourceStride,
                    IntPtr.Add(framebuffer.Address, row * destinationStride),
                    rowBytes
                );
            }
        }

        DisposeFrozenPreviewBitmap();
        _frozenPreviewBitmap = bitmap;
        _windowScreenshot.Source = bitmap;
        _windowScreenshot.IsVisible = true;
        _showingFrozenPreview = true;
        UpdateLayout();
    }

    private void ClearFrozenPreview()
    {
        if (!_showingFrozenPreview && _frozenPreviewBitmap is null)
            return;

        if (!_floatingPreviewPolicy.ShowScreenshotControl)
        {
            _windowScreenshot.IsVisible = false;
            _windowScreenshot.Source = null;
        }

        DisposeFrozenPreviewBitmap();
        _showingFrozenPreview = false;
        _frozenSnapshotReady = false;
    }

    private void DisposeFrozenPreviewBitmap()
    {
        if (
            ReferenceEquals(_windowScreenshot.Source, _frozenPreviewBitmap)
            && !_floatingPreviewPolicy.ShowScreenshotControl
        )
        {
            _windowScreenshot.Source = null;
        }

        _frozenPreviewBitmap?.Dispose();
        _frozenPreviewBitmap = null;
    }

    private void DisposePendingFrame()
    {
        PreviewFrame? frame;
        lock (_pendingFrameSync)
        {
            frame = _pendingFrame;
            _pendingFrame = null;
        }
        frame?.Dispose();
    }

    private WriteableBitmap? GetNextStreamBitmap(int widthPx, int heightPx)
    {
        if (widthPx <= 0 || heightPx <= 0)
            return null;
        var size = new PixelSize(widthPx, heightPx);
        if (_streamBitmapSize != size)
        {
            _windowScreenshot.Source = null;
            DisposeStreamBitmaps();
            _streamBitmapSize = size;
            _streamBitmapA = CreateStreamBitmap(size);
            _streamBitmapB = CreateStreamBitmap(size);
        }
        WriteableBitmap? bitmap = _nextStreamBitmapIndex == 0 ? _streamBitmapA : _streamBitmapB;
        _nextStreamBitmapIndex = _nextStreamBitmapIndex == 0 ? 1 : 0;
        return bitmap;
    }

    private static WriteableBitmap CreateStreamBitmap(PixelSize size) =>
        new(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    private void DisposeStreamBitmaps()
    {
        _streamBitmapA?.Dispose();
        _streamBitmapB?.Dispose();
        _streamBitmapA = null;
        _streamBitmapB = null;
        _streamBitmapSize = default;
        _nextStreamBitmapIndex = 0;
    }

    private CancellationTokenSource CreatePreviewOperationTokenSource(
        CancellationToken cancellationToken
    )
    {
        CancellationToken operationToken;
        lock (_previewOperationCancellationSync)
            operationToken = _previewOperationCancellation?.Token ?? CancellationToken.None;
        return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationToken);
    }

    private void CancelPreviewOperations(bool recreateTokenSource)
    {
        CancellationTokenSource? cancellation;
        lock (_previewOperationCancellationSync)
        {
            cancellation = _previewOperationCancellation;
            _previewOperationCancellation = recreateTokenSource
                ? new CancellationTokenSource()
                : null;
        }
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The source was already disposed, so there is nothing left to cancel.
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private void ScheduleResizePreviewRefresh()
    {
        var cancellation = new CancellationTokenSource();
        CancellationTokenSource? previous;
        lock (_resizePreviewRefreshSync)
        {
            previous = _resizePreviewRefreshCancellation;
            _resizePreviewRefreshCancellation = cancellation;
            _resizePreviewRefreshPending = true;
        }
        previous?.Cancel();
        _ = RestartAfterResizeAsync(cancellation);
    }

    private async Task RestartAfterResizeAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(ResizePreviewRefreshDebounceMs, cancellation.Token)
                .ConfigureAwait(false);
            if (TryConsumePendingResizePreviewRefresh(cancellation))
                CancelPreviewOperations(recreateTokenSource: true);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer resize or cancelled on close: this refresh is dropped.
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void FlushPendingResizePreviewRefresh() =>
        CancelPendingResizePreviewRefresh(executeRefresh: true);

    private void CancelPendingResizePreviewRefresh(bool executeRefresh)
    {
        CancellationTokenSource? cancellation;
        bool refresh;
        lock (_resizePreviewRefreshSync)
        {
            cancellation = _resizePreviewRefreshCancellation;
            _resizePreviewRefreshCancellation = null;
            refresh = _resizePreviewRefreshPending;
            _resizePreviewRefreshPending = false;
        }
        cancellation?.Cancel();
        if (executeRefresh && refresh && !_isClosing)
            CancelPreviewOperations(recreateTokenSource: true);
    }

    private bool TryConsumePendingResizePreviewRefresh(CancellationTokenSource cancellation)
    {
        lock (_resizePreviewRefreshSync)
        {
            if (!ReferenceEquals(_resizePreviewRefreshCancellation, cancellation))
                return false;
            _resizePreviewRefreshCancellation = null;
            bool refresh = _resizePreviewRefreshPending && !_isClosing;
            _resizePreviewRefreshPending = false;
            return refresh;
        }
    }

    private bool IsPreviewCaptureEnabled() => Volatile.Read(ref _previewCaptureEnabled) != 0;

    private static bool ReadPreviewCaptureEnabled() =>
        ConfigFileAccessor.GetInstance().ReadConfig(config => config.EnablePreviews);

    private void RegisterWindowThumbnail()
    {
        UnregisterWindowThumbnail();
        IPlatformHandle? platformHandle = _ownerWindow.TryGetPlatformHandle();
        if (platformHandle is null || !long.TryParse(_windowConfig.WindowId, out long sourceHandle))
            return;

        double scale = _ownerWindow.Screens.Primary?.Scaling ?? 1;
        int inset = (int)Math.Round(PreviewBorderThickness * scale);
        var bounds = new NativeThumbnailBounds(
            inset,
            (int)(TitleReservedHeight * scale) + inset,
            (int)(_windowConfig.WindowWidth * scale) - inset,
            (int)(_windowConfig.WindowHeight * scale) - inset
        );
        // Window.Opacity on FloatingWindow provides translucency; keep DWM thumbnail solid.
        byte opacity = FloatingPreviewHoverAdjustment.ToByteOpacity(
            FloatingPreviewHoverAdjustment.MaxOpacity
        );
        if (
            _nativeThumbnailRenderer.TryRegister(
                platformHandle.Handle,
                new IntPtr(sourceHandle),
                bounds,
                opacity,
                out nint handle
            )
        )
            _thumbnailHandle = handle;
    }

    private void UnregisterWindowThumbnail()
    {
        if (_thumbnailHandle == 0)
            return;
        _nativeThumbnailRenderer.Unregister(_thumbnailHandle);
        _thumbnailHandle = 0;
    }

    private double RoundToPixel(double value)
    {
        double scale = _ownerWindow.RenderScaling > 0 ? _ownerWindow.RenderScaling : 1;
        return Math.Round(value * scale) / scale;
    }
}
