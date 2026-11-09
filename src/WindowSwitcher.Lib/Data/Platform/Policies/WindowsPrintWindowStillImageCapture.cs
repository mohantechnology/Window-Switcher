using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;
using WindowSwitcher.Lib.Data.Platform.Interop;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;
using WindowSwitcher.Lib.Models;

namespace WindowSwitcher.Lib.Data.Platform.Policies;

/// <summary>
/// Captures a still BGRA image of a Win32 window via PrintWindow for frozen previews.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsPrintWindowStillImageCapture : IWindowStillImageCapture
{
    private static readonly ILogger Logger = Log.ForContext<WindowsPrintWindowStillImageCapture>();

    /// <inheritdoc />
    public bool TryCapture(
        nint windowHandle,
        int? maxWidthPx,
        int? maxHeightPx,
        out WindowStillImage? image
    )
    {
        image = null;
        if (windowHandle == 0)
            return false;

        if (!User32Functions.GetWindowRect(new IntPtr(windowHandle), out User32Functions.Rect rect))
            return false;

        int sourceWidth = Math.Max(0, rect.Right - rect.Left);
        int sourceHeight = Math.Max(0, rect.Bottom - rect.Top);
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return false;

        (int outputWidth, int outputHeight) = ResolveOutputSize(
            sourceWidth,
            sourceHeight,
            maxWidthPx,
            maxHeightPx
        );

        IntPtr screenDc = Gdi32Functions.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
            return false;

        IntPtr sourceDc = IntPtr.Zero;
        IntPtr sourceBitmap = IntPtr.Zero;
        IntPtr sourceBits = IntPtr.Zero;
        IntPtr previousSource = IntPtr.Zero;
        IntPtr outputDc = IntPtr.Zero;
        IntPtr outputBitmap = IntPtr.Zero;
        IntPtr outputBits = IntPtr.Zero;
        IntPtr previousOutput = IntPtr.Zero;
        try
        {
            sourceDc = Gdi32Functions.CreateCompatibleDC(screenDc);
            if (sourceDc == IntPtr.Zero)
                return false;

            var sourceInfo = CreateTopDownBgraInfo(sourceWidth, sourceHeight);
            sourceBitmap = Gdi32Functions.CreateDIBSection(
                sourceDc,
                ref sourceInfo,
                usage: 0,
                out sourceBits,
                IntPtr.Zero,
                offset: 0
            );
            if (sourceBitmap == IntPtr.Zero || sourceBits == IntPtr.Zero)
                return false;

            previousSource = Gdi32Functions.SelectObject(sourceDc, sourceBitmap);
            if (
                !User32Functions.PrintWindow(
                    new IntPtr(windowHandle),
                    sourceDc,
                    Gdi32Functions.PwRenderFullContent
                )
            )
            {
                Logger.Warning("PrintWindow failed while freezing preview for window handle");
                return false;
            }

            int sourceStride = checked(sourceWidth * 4);
            if (outputWidth == sourceWidth && outputHeight == sourceHeight)
            {
                image = CopyBits(sourceBits, sourceWidth, sourceHeight, sourceStride);
                return image is not null;
            }

            outputDc = Gdi32Functions.CreateCompatibleDC(screenDc);
            if (outputDc == IntPtr.Zero)
                return false;

            var outputInfo = CreateTopDownBgraInfo(outputWidth, outputHeight);
            outputBitmap = Gdi32Functions.CreateDIBSection(
                outputDc,
                ref outputInfo,
                usage: 0,
                out outputBits,
                IntPtr.Zero,
                offset: 0
            );
            if (outputBitmap == IntPtr.Zero || outputBits == IntPtr.Zero)
                return false;

            previousOutput = Gdi32Functions.SelectObject(outputDc, outputBitmap);
            _ = Gdi32Functions.SetStretchBltMode(outputDc, Gdi32Functions.Halftone);
            if (
                !Gdi32Functions.StretchBlt(
                    outputDc,
                    0,
                    0,
                    outputWidth,
                    outputHeight,
                    sourceDc,
                    0,
                    0,
                    sourceWidth,
                    sourceHeight,
                    Gdi32Functions.SrcCopy
                )
            )
            {
                return false;
            }

            int outputStride = checked(outputWidth * 4);
            image = CopyBits(outputBits, outputWidth, outputHeight, outputStride);
            return image is not null;
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Still-image capture failed for a floating preview freeze");
            image = null;
            return false;
        }
        finally
        {
            if (previousOutput != IntPtr.Zero && outputDc != IntPtr.Zero)
                Gdi32Functions.SelectObject(outputDc, previousOutput);
            if (outputBitmap != IntPtr.Zero)
                Gdi32Functions.DeleteObject(outputBitmap);
            if (outputDc != IntPtr.Zero)
                Gdi32Functions.DeleteDC(outputDc);

            if (previousSource != IntPtr.Zero && sourceDc != IntPtr.Zero)
                Gdi32Functions.SelectObject(sourceDc, previousSource);
            if (sourceBitmap != IntPtr.Zero)
                Gdi32Functions.DeleteObject(sourceBitmap);
            if (sourceDc != IntPtr.Zero)
                Gdi32Functions.DeleteDC(sourceDc);

            _ = Gdi32Functions.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static Gdi32Functions.BitmapInfo CreateTopDownBgraInfo(int width, int height)
    {
        return new Gdi32Functions.BitmapInfo
        {
            bmiHeader = new Gdi32Functions.BitmapInfoHeader
            {
                biSize = Marshal.SizeOf<Gdi32Functions.BitmapInfoHeader>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Gdi32Functions.BiRgb,
            },
        };
    }

    private static (int Width, int Height) ResolveOutputSize(
        int sourceWidth,
        int sourceHeight,
        int? maxWidthPx,
        int? maxHeightPx
    )
    {
        int maxWidth = maxWidthPx is > 0 ? maxWidthPx.Value : sourceWidth;
        int maxHeight = maxHeightPx is > 0 ? maxHeightPx.Value : sourceHeight;
        if (sourceWidth <= maxWidth && sourceHeight <= maxHeight)
            return (sourceWidth, sourceHeight);

        double widthScale = maxWidth / (double)sourceWidth;
        double heightScale = maxHeight / (double)sourceHeight;
        double scale = Math.Min(widthScale, heightScale);
        int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return (width, height);
    }

    private static WindowStillImage? CopyBits(IntPtr bits, int width, int height, int stride)
    {
        int length = checked(stride * height);
        var pixels = new byte[length];
        Marshal.Copy(bits, pixels, 0, length);
        return new WindowStillImage(width, height, stride, pixels);
    }
}
