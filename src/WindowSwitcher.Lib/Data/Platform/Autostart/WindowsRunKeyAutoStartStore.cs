using System.Runtime.Versioning;
using Microsoft.Win32;
using WindowSwitcher.Lib.Data.Platform.Autostart.Abstractions;

namespace WindowSwitcher.Lib.Data.Platform.Autostart;

/// <summary>
/// Persists Windows login auto-start under the current-user Run key.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRunKeyAutoStartStore : IAutoStartRegistrationStore
{
    /// <summary>
    /// Registry value name used for Window Switcher auto-start.
    /// </summary>
    public const string RunValueName = "WindowSwitcher";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly Func<RegistryKey?> _openWritableRunKey;
    private readonly Func<RegistryKey?> _openReadableRunKey;

    /// <summary>
    /// Creates a store that reads and writes the current-user Run key.
    /// </summary>
    public WindowsRunKeyAutoStartStore()
        : this(OpenWritableRunKey, OpenReadableRunKey) { }

    /// <summary>
    /// Creates a store with injectable registry key openers for tests.
    /// </summary>
    public WindowsRunKeyAutoStartStore(
        Func<RegistryKey?> openWritableRunKey,
        Func<RegistryKey?> openReadableRunKey
    )
    {
        ArgumentNullException.ThrowIfNull(openWritableRunKey);
        ArgumentNullException.ThrowIfNull(openReadableRunKey);

        _openWritableRunKey = openWritableRunKey;
        _openReadableRunKey = openReadableRunKey;
    }

    /// <inheritdoc />
    public bool Exists()
    {
        using RegistryKey? key = _openReadableRunKey();
        return key?.GetValue(RunValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    /// <inheritdoc />
    public string? ReadCommand()
    {
        using RegistryKey? key = _openReadableRunKey();
        return key?.GetValue(RunValueName) as string;
    }

    /// <inheritdoc />
    public void WriteCommand(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        using RegistryKey key =
            _openWritableRunKey()
            ?? throw new InvalidOperationException(
                "The Windows Run registry key could not be opened for writing."
            );
        key.SetValue(RunValueName, command);
    }

    /// <inheritdoc />
    public void Delete()
    {
        using RegistryKey? key = _openWritableRunKey();
        key?.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    private static RegistryKey? OpenWritableRunKey()
    {
        return Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
    }

    private static RegistryKey? OpenReadableRunKey()
    {
        return Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
    }
}
