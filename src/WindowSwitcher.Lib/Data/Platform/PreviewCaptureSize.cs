namespace WindowSwitcher.Lib.Data.Platform;

/// <summary>
/// Chooses a thumbnail capture size that stays at least 1080p.
/// </summary>
public static class PreviewCaptureSize
{
    /// <summary>
    /// Minimum capture width, in pixels. 1920 is the width of a 1080p frame.
    /// </summary>
    public const int MinimumWidth = 1920;

    /// <summary>
    /// Minimum capture height, in pixels. 1080 is the height of a 1080p frame.
    /// </summary>
    public const int MinimumHeight = 1080;

    /// <summary>
    /// Scales a requested frame up, preserving its aspect ratio, until both sides meet 1080p.
    /// A request that is already at least 1080p is returned unchanged.
    /// </summary>
    /// <param name="width">Requested width in pixels.</param>
    /// <param name="height">Requested height in pixels.</param>
    public static (int Width, int Height) AtLeastFullHd(int width, int height)
    {
        int safeWidth = Math.Max(1, width);
        int safeHeight = Math.Max(1, height);
        if (safeWidth >= MinimumWidth && safeHeight >= MinimumHeight)
            return (safeWidth, safeHeight);

        double scale = Math.Max(
            MinimumWidth / (double)safeWidth,
            MinimumHeight / (double)safeHeight
        );
        return (
            Math.Max(1, (int)Math.Round(safeWidth * scale, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(safeHeight * scale, MidpointRounding.AwayFromZero))
        );
    }
}
