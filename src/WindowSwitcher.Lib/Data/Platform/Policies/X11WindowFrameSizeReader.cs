using System.Globalization;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;
using WindowSwitcher.Lib.Data.Platform.WindowAccess.PreviewFrames.X11;

namespace WindowSwitcher.Lib.Data.Platform.Policies;

/// <summary>
/// Reads an X11 window's size. Wayland ids that are not X11 window ids return false.
/// </summary>
public sealed class X11WindowFrameSizeReader : IWindowFrameSizeReader
{
    /// <inheritdoc />
    public bool TryGetFrameSize(string windowId, out int widthPx, out int heightPx)
    {
        widthPx = 0;
        heightPx = 0;
        if (!TryParseWindowId(windowId, out nint window))
            return false;

        X11Native.EnsureInitialized();
        nint display = X11Native.XOpenDisplay(nint.Zero);
        if (display == nint.Zero)
            return false;

        try
        {
            if (X11Native.XGetWindowAttributes(display, window, out XWindowAttributes attributes) == 0)
                return false;
            if (attributes.Width <= 0 || attributes.Height <= 0)
                return false;

            widthPx = attributes.Width;
            heightPx = attributes.Height;
            return true;
        }
        finally
        {
            _ = X11Native.XCloseDisplay(display);
        }
    }

    private static bool TryParseWindowId(string windowId, out nint window)
    {
        window = nint.Zero;
        if (string.IsNullOrWhiteSpace(windowId))
            return false;

        string value = windowId.Trim();
        NumberStyles styles = NumberStyles.Integer;
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
            styles = NumberStyles.HexNumber;
        }

        if (!ulong.TryParse(value, styles, CultureInfo.InvariantCulture, out ulong parsed))
            return false;
        if (parsed == 0 || parsed > long.MaxValue)
            return false;

        window = new nint(unchecked((long)parsed));
        return true;
    }
}
