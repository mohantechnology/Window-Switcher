namespace WindowSwitcher.Lib.Data.Platform.Autostart.Abstractions;

/// <summary>
/// Persistence backend for login auto-start registration.
/// </summary>
public interface IAutoStartRegistrationStore
{
    /// <summary>
    /// Returns whether a registration entry currently exists.
    /// </summary>
    bool Exists();

    /// <summary>
    /// Reads the registered command line, or <c>null</c> when absent.
    /// </summary>
    string? ReadCommand();

    /// <summary>
    /// Writes or replaces the registration command line.
    /// </summary>
    void WriteCommand(string command);

    /// <summary>
    /// Removes the registration entry when present.
    /// </summary>
    void Delete();
}
