using System.Runtime.Versioning;
using WindowSwitcher.Lib.Data.Platform.Interop;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

namespace WindowSwitcher.Lib.Data.Platform.Policies;

/// <summary>
/// Reads a Win32 window's outer size.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsWindowFrameSizeReader : IWindowFrameSizeReader
{
    /// <inheritdoc />
    public bool TryGetFrameSize(string windowId, out int widthPx, out int heightPx)
    {
        widthPx = 0;
        heightPx = 0;
        if (!long.TryParse(windowId, out long parsed) || parsed == 0)
            return false;

        if (!User32Functions.GetWindowRect(new nint(parsed), out User32Functions.Rect rect))
            return false;

        widthPx = rect.Right - rect.Left;
        heightPx = rect.Bottom - rect.Top;
        return widthPx > 0 && heightPx > 0;
    }
}
