namespace WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

/// <summary>
/// Configures a floating window native handle for platform-specific behavior.
/// </summary>
public interface IFloatingWindowHandleConfigurator
{
    /// <summary>
    /// Applies platform-specific window handle configuration.
    /// </summary>
    void Configure(nint windowHandle);

    /// <summary>
    /// Sets the whole floating window's translucency so content beneath remains visible.
    /// </summary>
    /// <param name="windowHandle">Native window handle.</param>
    /// <param name="opacity">Normalized opacity in the range 0-1.</param>
    void SetOpacity(nint windowHandle, double opacity);
}
