using System.Runtime.Versioning;
using WindowSwitcher.Lib.Data.Platform.Interop;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

namespace WindowSwitcher.Lib.Data.Platform.Policies;

/// <summary>
/// Reads Win key state via <c>GetAsyncKeyState</c> because Avalonia pointer
/// events often omit <c>KeyModifiers.Meta</c> while Win is held on Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsMetaKeyStateReader : IMetaKeyStateReader
{
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    /// <inheritdoc />
    public bool IsMetaPressed()
    {
        return IsKeyDown(VkLWin) || IsKeyDown(VkRWin);
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (User32Functions.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }
}
