namespace WindowSwitcher.Lib.Data.Platform;

/// <summary>
/// Keeps a floating preview's width and height in the source window's aspect ratio.
/// </summary>
public static class LinkedPreviewSize
{
    /// <summary>
    /// Returns the width/height ratio when both sides are positive.
    /// </summary>
    public static bool TryAspect(int width, int height, out double aspectWidthOverHeight)
    {
        if (width <= 0 || height <= 0)
        {
            aspectWidthOverHeight = 0;
            return false;
        }

        aspectWidthOverHeight = width / (double)height;
        return true;
    }

    /// <summary>
    /// Keeps <paramref name="width" /> and sets the height so the content area matches
    /// <paramref name="aspectWidthOverHeight" />. <paramref name="reservedHeight" /> is the
    /// title strip that is not part of the picture.
    /// </summary>
    public static (int Width, int Height) FitWidth(
        int width,
        double aspectWidthOverHeight,
        int reservedHeight = 0
    )
    {
        int safeWidth = Clamp(width);
        int contentHeight = ContentHeight(safeWidth, aspectWidthOverHeight);
        int height = Clamp(contentHeight + Math.Max(0, reservedHeight));
        if (height - Math.Max(0, reservedHeight) != contentHeight)
        {
            int fittedContent = Math.Max(1, height - Math.Max(0, reservedHeight));
            safeWidth = Clamp(ContentWidth(fittedContent, aspectWidthOverHeight));
            height = Clamp(ContentHeight(safeWidth, aspectWidthOverHeight) + Math.Max(0, reservedHeight));
        }

        return (safeWidth, height);
    }

    /// <summary>
    /// Adjusts one side so the content area keeps <paramref name="aspectWidthOverHeight" />.
    /// </summary>
    public static (int Width, int Height) Preserve(
        int width,
        int height,
        double aspectWidthOverHeight,
        bool widthDrives,
        int reservedHeight = 0
    )
    {
        if (widthDrives)
            return FitWidth(width, aspectWidthOverHeight, reservedHeight);

        int reserved = Math.Max(0, reservedHeight);
        int safeHeight = Clamp(height);
        int contentHeight = Math.Max(1, safeHeight - reserved);
        int safeWidth = Clamp(ContentWidth(contentHeight, aspectWidthOverHeight));
        return FitWidth(safeWidth, aspectWidthOverHeight, reserved);
    }

    /// <summary>
    /// Scales the preview and then snaps it back to the linked aspect ratio.
    /// </summary>
    public static (int Width, int Height) Scale(
        int width,
        int height,
        bool increase,
        double aspectWidthOverHeight,
        int reservedHeight = 0
    )
    {
        (int scaledWidth, _) = FloatingPreviewHoverAdjustment.ScaleSize(width, height, increase);
        return FitWidth(scaledWidth, aspectWidthOverHeight, reservedHeight);
    }

    private static int ContentWidth(int contentHeight, double aspectWidthOverHeight)
    {
        double aspect = SanitizeAspect(aspectWidthOverHeight);
        return Math.Max(1, (int)Math.Round(contentHeight * aspect, MidpointRounding.AwayFromZero));
    }

    private static int ContentHeight(int width, double aspectWidthOverHeight)
    {
        double aspect = SanitizeAspect(aspectWidthOverHeight);
        return Math.Max(1, (int)Math.Round(width / aspect, MidpointRounding.AwayFromZero));
    }

    private static double SanitizeAspect(double aspectWidthOverHeight)
    {
        if (aspectWidthOverHeight is > 0.05 and < 20)
            return aspectWidthOverHeight;
        return 1;
    }

    private static int Clamp(int size) =>
        Math.Clamp(
            size,
            FloatingPreviewHoverAdjustment.MinSize,
            FloatingPreviewHoverAdjustment.MaxSize
        );
}
