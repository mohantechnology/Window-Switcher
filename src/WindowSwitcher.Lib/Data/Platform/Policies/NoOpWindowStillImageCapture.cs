using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;
using WindowSwitcher.Lib.Models;

namespace WindowSwitcher.Lib.Data.Platform.Policies;

/// <summary>
/// No-op still-image capture used on platforms that already keep the last streamed frame.
/// </summary>
public sealed class NoOpWindowStillImageCapture : IWindowStillImageCapture
{
    /// <inheritdoc />
    public bool TryCapture(
        nint windowHandle,
        int? maxWidthPx,
        int? maxHeightPx,
        out WindowStillImage? image
    )
    {
        image = null;
        return false;
    }
}
