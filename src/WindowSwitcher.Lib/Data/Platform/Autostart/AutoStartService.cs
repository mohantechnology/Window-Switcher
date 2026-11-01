using Serilog;
using WindowSwitcher.Lib.Data.Platform.Autostart.Abstractions;

namespace WindowSwitcher.Lib.Data.Platform.Autostart;

/// <summary>
/// Registers login auto-start using a platform-specific persistence store.
/// </summary>
public sealed class AutoStartService : IAutoStartService
{
    private readonly IAutoStartRegistrationStore _store;
    private readonly Func<string> _executablePathProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates an auto-start service that resolves the current process path automatically.
    /// </summary>
    public AutoStartService(IAutoStartRegistrationStore store, ILogger? logger = null)
        : this(store, ResolveExecutablePath, logger) { }

    /// <summary>
    /// Creates an auto-start service with an explicit executable path provider.
    /// </summary>
    public AutoStartService(
        IAutoStartRegistrationStore store,
        Func<string> executablePathProvider,
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(executablePathProvider);

        _store = store;
        _executablePathProvider = executablePathProvider;
        _logger = logger ?? Log.ForContext<AutoStartService>();
    }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.Exists());
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!enabled)
        {
            _store.Delete();
            return Task.CompletedTask;
        }

        string executablePath = _executablePathProvider();
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException(
                "The current executable path could not be resolved for auto-start registration."
            );

        string command = AutoStartCommandBuilder.Build(executablePath);
        _store.WriteCommand(command);
        _logger.Information("Login auto-start registration was updated");
        return Task.CompletedTask;
    }

    private static string ResolveExecutablePath()
    {
        string? processPath = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(processPath) ? string.Empty : processPath;
    }
}
