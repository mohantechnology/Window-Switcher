namespace WindowSwitcher.Lib.Models;

/// <summary>
/// Builds the name shown on a floating preview so the application stays visible.
/// </summary>
public static class FloatingWindowLabelText
{
    /// <summary>
    /// Maximum number of characters taken from the window title before the application name.
    /// </summary>
    public const int TitlePrefixLength = 15;

    private const int MaxCaptionAppNameLength = 40;
    private const string CaptionSeparator = " - ";
    private const string Ellipsis = "...";

    /// <summary>
    /// Formats a floating-window label from a window title and process name.
    /// Window captions are usually "document - Application". A long document used to
    /// push the application name out of view, so the document contributes at most
    /// <see cref="TitlePrefixLength"/> characters and the application name is kept.
    /// </summary>
    /// <param name="windowTitle">Window caption. <see langword="null"/> is treated as empty.</param>
    /// <param name="processName">
    /// Process name used when the caption does not end with an application name.
    /// </param>
    /// <returns>The label text, or an empty string when both inputs are blank.</returns>
    public static string Format(string? windowTitle, string? processName)
    {
        string title = Normalize(windowTitle);
        string process = Normalize(processName);
        SplitCaption(title, out string document, out string captionApp);
        string appName = captionApp.Length > 0 ? captionApp : process;
        string prefix = Truncate(document, TitlePrefixLength);

        if (appName.Length == 0)
            return prefix;
        if (prefix.Length == 0)
            return appName;
        if (string.Equals(prefix, appName, StringComparison.OrdinalIgnoreCase))
            return prefix;
        if (
            document.Length <= TitlePrefixLength
            && document.EndsWith(appName, StringComparison.OrdinalIgnoreCase)
        )
            return document;

        return $"{prefix}{CaptionSeparator}{appName}";
    }

    private static void SplitCaption(string title, out string document, out string appName)
    {
        document = title;
        appName = string.Empty;
        int separator = title.LastIndexOf(CaptionSeparator, StringComparison.Ordinal);
        if (separator <= 0)
            return;

        string leading = title[..separator].Trim();
        string trailing = title[(separator + CaptionSeparator.Length)..].Trim();
        if (leading.Length == 0 || trailing.Length == 0)
            return;

        // A very long suffix is more title text, not an application name.
        document = leading;
        if (trailing.Length <= MaxCaptionAppNameLength)
            appName = trailing;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        string prefix = value[..maxLength].TrimEnd();
        if (prefix.Length == 0)
            prefix = value[..maxLength];
        return prefix + Ellipsis;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
