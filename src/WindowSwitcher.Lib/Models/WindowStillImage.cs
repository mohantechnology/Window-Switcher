namespace WindowSwitcher.Lib.Models;

/// <summary>
/// A one-shot BGRA still image of a window used for frozen floating previews.
/// </summary>
public sealed class WindowStillImage
{
    /// <summary>
    /// Initializes a still image buffer.
    /// </summary>
    public WindowStillImage(int widthPx, int heightPx, int stride, byte[] bgraPixels)
    {
        ArgumentNullException.ThrowIfNull(bgraPixels);
        if (widthPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(widthPx));
        if (heightPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(heightPx));
        if (stride < widthPx * 4)
            throw new ArgumentOutOfRangeException(nameof(stride));
        if (bgraPixels.Length < stride * heightPx)
            throw new ArgumentException("Pixel buffer is smaller than stride * height.", nameof(bgraPixels));

        WidthPx = widthPx;
        HeightPx = heightPx;
        Stride = stride;
        BgraPixels = bgraPixels;
    }

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public int WidthPx { get; }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public int HeightPx { get; }

    /// <summary>
    /// Gets the number of bytes between consecutive rows.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Gets the BGRA pixel buffer.
    /// </summary>
    public byte[] BgraPixels { get; }
}
