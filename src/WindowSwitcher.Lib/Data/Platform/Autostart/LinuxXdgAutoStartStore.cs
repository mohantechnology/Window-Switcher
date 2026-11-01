using WindowSwitcher.Lib.Data.Platform.Autostart.Abstractions;

namespace WindowSwitcher.Lib.Data.Platform.Autostart;

/// <summary>
/// Persists Linux login auto-start as an XDG autostart desktop entry.
/// </summary>
public sealed class LinuxXdgAutoStartStore : IAutoStartRegistrationStore
{
    /// <summary>
    /// Desktop entry file name used for Window Switcher auto-start.
    /// </summary>
    public const string DesktopFileName = "windowswitcher.desktop";

    private readonly string _desktopFilePath;
    private readonly Func<string, string> _readAllText;
    private readonly Action<string, string> _writeAllText;
    private readonly Action<string> _deleteFile;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string> _createDirectory;

    /// <summary>
    /// Creates a store that writes under the user XDG autostart directory.
    /// </summary>
    public LinuxXdgAutoStartStore()
        : this(
            ResolveDesktopFilePath(),
            File.ReadAllText,
            File.WriteAllText,
            File.Delete,
            File.Exists,
            path => Directory.CreateDirectory(path)
        )
    { }

    /// <summary>
    /// Creates a store with injectable filesystem operations for tests.
    /// </summary>
    public LinuxXdgAutoStartStore(
        string desktopFilePath,
        Func<string, string> readAllText,
        Action<string, string> writeAllText,
        Action<string> deleteFile,
        Func<string, bool> fileExists,
        Action<string> createDirectory
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(desktopFilePath);
        ArgumentNullException.ThrowIfNull(readAllText);
        ArgumentNullException.ThrowIfNull(writeAllText);
        ArgumentNullException.ThrowIfNull(deleteFile);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(createDirectory);

        _desktopFilePath = desktopFilePath;
        _readAllText = readAllText;
        _writeAllText = writeAllText;
        _deleteFile = deleteFile;
        _fileExists = fileExists;
        _createDirectory = createDirectory;
    }

    /// <inheritdoc />
    public bool Exists() => _fileExists(_desktopFilePath);

    /// <inheritdoc />
    public string? ReadCommand()
    {
        if (!_fileExists(_desktopFilePath))
            return null;

        foreach (string line in _readAllText(_desktopFilePath).Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("Exec=", StringComparison.Ordinal))
                return trimmed["Exec=".Length..].Trim();
        }

        return null;
    }

    /// <inheritdoc />
    public void WriteCommand(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        string? directory = Path.GetDirectoryName(_desktopFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            _createDirectory(directory);

        string contents =
            $"""
            [Desktop Entry]
            Type=Application
            Version=1.0
            Name=Window Switcher
            Comment=Start Window Switcher in the background
            Exec={command}
            Terminal=false
            X-GNOME-Autostart-enabled=true
            """;

        _writeAllText(_desktopFilePath, contents);
    }

    /// <inheritdoc />
    public void Delete()
    {
        if (_fileExists(_desktopFilePath))
            _deleteFile(_desktopFilePath);
    }

    /// <summary>
    /// Resolves the XDG autostart desktop entry path for the current user.
    /// </summary>
    public static string ResolveDesktopFilePath()
    {
        string? xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string configHome = string.IsNullOrWhiteSpace(xdgConfigHome)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config"
            )
            : xdgConfigHome;

        return Path.Combine(configHome, "autostart", DesktopFileName);
    }
}
