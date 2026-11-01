using WindowSwitcher.Lib.Data.Platform.Keybinds.Models;

namespace WindowSwitcher.Lib.Data.Platform;

/// <summary>
/// Pure helpers for hover-gated size and opacity adjustments on floating previews.
/// </summary>
public static class FloatingPreviewHoverAdjustment
{
    /// <summary>
    /// Relative scale applied per mouse-wheel notch when changing size.
    /// </summary>
    public const double SizeScaleFactor = 1.10;

    /// <summary>
    /// Minimum floating preview width or height in device-independent pixels.
    /// </summary>
    public const int MinSize = 50;

    /// <summary>
    /// Maximum floating preview width or height in device-independent pixels.
    /// </summary>
    public const int MaxSize = 2000;

    /// <summary>
    /// Opacity delta applied per mouse-wheel notch.
    /// </summary>
    public const double OpacityStep = 0.05;

    /// <summary>
    /// Minimum preview opacity.
    /// </summary>
    public const double MinOpacity = 0.2;

    /// <summary>
    /// Maximum preview opacity.
    /// </summary>
    public const double MaxOpacity = 1.0;

    /// <summary>
    /// Scales width and height while preserving aspect ratio and clamping to bounds.
    /// </summary>
    public static (int Width, int Height) ScaleSize(int width, int height, bool increase)
    {
        double safeWidth = Math.Max(1, width);
        double safeHeight = Math.Max(1, height);
        double factor = increase ? SizeScaleFactor : 1d / SizeScaleFactor;

        int nextWidth = (int)Math.Round(safeWidth * factor, MidpointRounding.AwayFromZero);
        int nextHeight = (int)Math.Round(safeHeight * factor, MidpointRounding.AwayFromZero);

        nextWidth = Math.Clamp(nextWidth, MinSize, MaxSize);
        nextHeight = Math.Clamp(nextHeight, MinSize, MaxSize);
        return (nextWidth, nextHeight);
    }

    /// <summary>
    /// Adjusts opacity by one step and clamps to the supported range.
    /// </summary>
    public static double AdjustOpacity(double opacity, bool increase)
    {
        double next = increase ? opacity + OpacityStep : opacity - OpacityStep;
        return Math.Clamp(next, MinOpacity, MaxOpacity);
    }

    /// <summary>
    /// Converts a normalized opacity (0-1) to a DWM/byte opacity value.
    /// </summary>
    public static byte ToByteOpacity(double opacity)
    {
        double clamped = Math.Clamp(opacity, 0d, MaxOpacity);
        return (byte)Math.Clamp((int)Math.Round(clamped * 255d, MidpointRounding.AwayFromZero), 0, 255);
    }

    /// <summary>
    /// Returns whether pointer modifiers match a mouse-wheel shortcut combination.
    /// </summary>
    public static bool MatchesMouseWheelShortcut(
        KeyCombination? combination,
        bool ctrl,
        bool alt,
        bool shift,
        bool meta
    )
    {
        if (combination is null || combination.Key != KeybindPrimaryKey.MouseWheel)
            return false;

        return combination.Ctrl == ctrl
            && combination.Alt == alt
            && combination.Shift == shift
            && combination.Meta == meta;
    }
}
