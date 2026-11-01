namespace WindowSwitcher.Lib.Data.Platform.Autostart.Abstractions;

/// <summary>
/// Registers or removes login-time auto-start for Window Switcher.
/// </summary>
public interface IAutoStartService
{
    /// <summary>
    /// Returns whether auto-start is currently registered with the operating system.
    /// </summary>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables operating-system login auto-start for the current executable.
    /// </summary>
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
