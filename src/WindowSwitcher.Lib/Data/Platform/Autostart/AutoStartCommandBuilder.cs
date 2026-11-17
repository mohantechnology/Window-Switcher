namespace WindowSwitcher.Lib.Data.Platform.Autostart;

/// <summary>
/// Builds the command line used for login auto-start registration.
/// </summary>
public static class AutoStartCommandBuilder
{
    /// <summary>
    /// Argument appended so login launches stay hidden in the tray.
    /// </summary>
    public const string BackgroundArgument = "--background";

    /// <summary>
    /// Builds a quoted executable command that launches in the background.
    /// </summary>
    public static string Build(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        string trimmedPath = executablePath.Trim();
        return $"\"{trimmedPath}\" {BackgroundArgument}";
    }
}
