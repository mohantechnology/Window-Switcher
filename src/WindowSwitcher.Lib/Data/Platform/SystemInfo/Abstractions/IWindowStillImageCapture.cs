using WindowSwitcher.Lib.Models;

namespace WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

/// <summary>
/// Captures a one-shot still image of a top-level window for frozen preview display.
/// </summary>
public interface IWindowStillImageCapture
{
    /// <summary>
    /// Attempts to capture the current contents of a window.
    /// </summary>
    /// <param name="windowHandle">Native handle of the window to capture.</param>
    /// <param name="maxWidthPx">Optional maximum output width in pixels.</param>
    /// <param name="maxHeightPx">Optional maximum output height in pixels.</param>
    /// <param name="image">Captured BGRA image when the call succeeds.</param>
    /// <returns><see langword="true"/> when a still image was captured.</returns>
    bool TryCapture(
        nint windowHandle,
        int? maxWidthPx,
        int? maxHeightPx,
        out WindowStillImage? image
    );
}
