namespace WindowSwitcher.Lib.Models;

/// <summary>
/// Size of the name label drawn above a floating preview.
/// </summary>
public static class FloatingWindowLabelLayout
{
    /// <summary>
    /// Font size used when the user has not chosen one.
    /// </summary>
    public const int DefaultFontSize = 10;

    /// <summary>
    /// Smallest supported label font size.
    /// </summary>
    public const int MinFontSize = 8;

    /// <summary>
    /// Largest supported label font size.
    /// </summary>
    public const int MaxFontSize = 32;

    /// <summary>
    /// Clamps a label font size to the supported range.
    /// </summary>
    /// <param name="fontSize">Requested font size.</param>
    /// <returns>A font size from <see cref="MinFontSize"/> through <see cref="MaxFontSize"/>.</returns>
    public static int ClampFontSize(int fontSize) => Math.Clamp(fontSize, MinFontSize, MaxFontSize);

    /// <summary>
    /// Height reserved above the thumbnail for the window name.
    /// A font size of 10 reserves 26px.
    /// </summary>
    /// <param name="fontSize">Label font size, clamped to the supported range.</param>
    /// <returns>The title band height in device-independent pixels.</returns>
    public static int TitleBandHeight(int fontSize) => ClampFontSize(fontSize) + 16;
}
