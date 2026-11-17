namespace WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

/// <summary>
/// Reads the current outer size of a top-level window.
/// </summary>
public interface IWindowFrameSizeReader
{
    /// <summary>
    /// Attempts to read the window's outer width and height in physical pixels.
    /// </summary>
    /// <param name="windowId">Platform window id.</param>
    /// <param name="widthPx">Outer width when the call succeeds.</param>
    /// <param name="heightPx">Outer height when the call succeeds.</param>
    /// <returns><see langword="true" /> when both sides were read.</returns>
    bool TryGetFrameSize(string windowId, out int widthPx, out int heightPx);
}
