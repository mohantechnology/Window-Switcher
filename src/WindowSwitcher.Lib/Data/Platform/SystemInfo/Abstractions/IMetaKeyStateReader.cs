namespace WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

/// <summary>
/// Reads whether the platform Meta/Win/Super modifier is currently pressed.
/// </summary>
public interface IMetaKeyStateReader
{
    /// <summary>
    /// Returns whether Meta (Win/Super) is currently held down.
    /// </summary>
    bool IsMetaPressed();
}
