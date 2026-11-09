namespace WindowSwitcher.Lib.Models;

/// <summary>
/// Decides whether a single floating preview shows a live image.
/// </summary>
public static class FloatingPreviewPreference
{
    /// <summary>
    /// Resolves the effective live-preview state for one window.
    /// A window override wins. <see langword="null"/> follows the global setting.
    /// </summary>
    /// <param name="windowOverride">
    /// Per-window choice, or <see langword="null"/> to follow <paramref name="globalEnabled"/>.
    /// </param>
    /// <param name="globalEnabled">The global Enable previews setting.</param>
    /// <returns><see langword="true"/> when this preview should stay live.</returns>
    public static bool IsEnabled(bool? windowOverride, bool globalEnabled) =>
        windowOverride ?? globalEnabled;
}
