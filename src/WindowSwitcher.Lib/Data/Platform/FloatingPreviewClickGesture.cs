namespace WindowSwitcher.Lib.Data.Platform;

/// <summary>
/// Decides whether a floating-preview pointer gesture should focus the source window.
/// </summary>
public static class FloatingPreviewClickGesture
{
    /// <summary>
    /// Maximum left-button hold, in milliseconds, that still counts as a click.
    /// A hold of this length or longer is treated as a drag.
    /// </summary>
    public const int ClickHoldThresholdMilliseconds = 400;

    /// <summary>
    /// Movement, in pixels, above which a gesture is a drag even when it ends quickly.
    /// </summary>
    public const int DragThresholdPixels = 4;

    /// <summary>
    /// Returns whether the preview moved farther than <see cref="DragThresholdPixels" />.
    /// </summary>
    /// <param name="startX">Horizontal position when the button went down.</param>
    /// <param name="startY">Vertical position when the button went down.</param>
    /// <param name="endX">Horizontal position to compare.</param>
    /// <param name="endY">Vertical position to compare.</param>
    public static bool ExceedsDragThreshold(int startX, int startY, int endX, int endY)
    {
        long deltaX = Math.Abs((long)endX - startX);
        long deltaY = Math.Abs((long)endY - startY);
        return deltaX > DragThresholdPixels || deltaY > DragThresholdPixels;
    }

    /// <summary>
    /// Returns whether a left-button release should focus the source window.
    /// </summary>
    /// <param name="holdDurationMilliseconds">Time the button was held.</param>
    /// <param name="startX">Preview position when the button went down.</param>
    /// <param name="startY">Preview position when the button went down.</param>
    /// <param name="endX">Preview position when the button was released.</param>
    /// <param name="endY">Preview position when the button was released.</param>
    /// <param name="resizeDragStarted">Whether the gesture began as a resize.</param>
    /// <param name="movedBeyondThreshold">
    /// Whether the preview crossed the drag threshold at any point during the gesture.
    /// </param>
    public static bool IsActivationClick(
        long holdDurationMilliseconds,
        int startX,
        int startY,
        int endX,
        int endY,
        bool resizeDragStarted,
        bool movedBeyondThreshold
    )
    {
        if (resizeDragStarted || movedBeyondThreshold)
            return false;

        if (
            holdDurationMilliseconds < 0
            || holdDurationMilliseconds >= ClickHoldThresholdMilliseconds
        )
            return false;

        return !ExceedsDragThreshold(startX, startY, endX, endY);
    }
}
