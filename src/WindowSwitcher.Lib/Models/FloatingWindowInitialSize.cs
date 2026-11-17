namespace WindowSwitcher.Lib.Models;

/// <summary>
/// Chooses the width and height of a floating preview when it is created.
/// </summary>
public static class FloatingWindowInitialSize
{
    /// <summary>
    /// Width used when a new preview has no saved layout and no configured default.
    /// </summary>
    public const int FallbackWidth = 300;

    /// <summary>
    /// Height used when a new preview has no saved layout and no configured default.
    /// </summary>
    public const int FallbackHeight = 200;

    /// <summary>
    /// Resolves the size to store for a preview that is about to be shown.
    /// Fixed size wins over a saved layout. A saved layout wins over the default size.
    /// </summary>
    /// <param name="hasPersistedLayout">Whether this window already has a saved layout.</param>
    /// <param name="persistedWidth">Saved width, ignored when there is no saved layout.</param>
    /// <param name="persistedHeight">Saved height, ignored when there is no saved layout.</param>
    /// <param name="useDefaultWindowSize">Whether new previews start at the configured size.</param>
    /// <param name="useFixedWindowSize">Whether every preview uses the configured size.</param>
    /// <param name="configuredWidth">Width from settings.</param>
    /// <param name="configuredHeight">Height from settings.</param>
    public static (int Width, int Height) Resolve(
        bool hasPersistedLayout,
        int persistedWidth,
        int persistedHeight,
        bool useDefaultWindowSize,
        bool useFixedWindowSize,
        int configuredWidth,
        int configuredHeight
    )
    {
        if (useFixedWindowSize)
            return (configuredWidth, configuredHeight);

        if (hasPersistedLayout)
            return (persistedWidth, persistedHeight);

        if (useDefaultWindowSize)
            return (configuredWidth, configuredHeight);

        return (FallbackWidth, FallbackHeight);
    }
}
