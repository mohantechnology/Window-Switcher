using WindowSwitcher.Lib.Data.Platform;
using WindowSwitcher.Lib.Data.Platform.Interop;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

namespace WindowSwitcher.Lib.Data.Platform.Policies;

/// <summary>
/// Configures Windows floating windows to stay hidden from Alt+Tab and supports window alpha.
/// </summary>
public sealed class WindowsFloatingWindowHandleConfigurator : IFloatingWindowHandleConfigurator
{
    public void Configure(nint windowHandle)
    {
        if (windowHandle == 0)
            return;

        User32Functions.HideFromAltTab(new IntPtr(windowHandle));
    }

    public void SetOpacity(nint windowHandle, double opacity)
    {
        if (windowHandle == 0)
            return;

        byte alpha = FloatingPreviewHoverAdjustment.ToByteOpacity(opacity);
        User32Functions.SetWindowOpacity(new IntPtr(windowHandle), alpha);
    }
}
