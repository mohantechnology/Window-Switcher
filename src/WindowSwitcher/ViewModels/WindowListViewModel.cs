using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using WindowSwitcher.Lib.Models;
using WindowSwitcher.ViewModels.Abstractions;

namespace WindowSwitcher.ViewModels;

public enum PinWindowResult
{
    Ignored,
    Added,
    AlreadyOpen,
}

public partial class WindowListViewModel : ObservableObject, IDisposable
{
    private const int WindowListRefreshIntervalMs = 250;
    private readonly CancellationTokenSource _cts = new();
    private readonly IWindowSnapshotProvider _windowSnapshotProvider;
    private readonly IWindowFilterSettingsProvider _windowFilterSettingsProvider;
    private readonly IViewModelDispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _pauseSync = new();
    private TaskCompletionSource _resumeTcs = CreateCompletedResumeSignal();
    private bool _isPaused;

    [ObservableProperty]
    private ObservableCollection<WindowConfig> _windowsConfigs = new();

    [ObservableProperty]
    private WindowConfig? _selectedWindow;

    public HashSet<string> TempWindowIdsBlacklist { get; } = new(StringComparer.Ordinal);

    public HashSet<string> PinnedWindowIds { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ClosedFloatingPreviewIds { get; } = new(StringComparer.Ordinal);

    public WindowListViewModel(
        IWindowSnapshotProvider windowSnapshotProvider,
        IWindowFilterSettingsProvider windowFilterSettingsProvider,
        IViewModelDispatcher dispatcher,
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(windowSnapshotProvider);
        ArgumentNullException.ThrowIfNull(windowFilterSettingsProvider);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _windowSnapshotProvider = windowSnapshotProvider;
        _windowFilterSettingsProvider = windowFilterSettingsProvider;
        _dispatcher = dispatcher;
        _logger = logger ?? Log.ForContext<WindowListViewModel>();
        _ = Task.Run(() => RunPeriodicTask(_cts.Token));
    }

    private async Task RunPeriodicTask(CancellationToken cancellationToken)
    {
        await RefreshWindowsAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(WindowListRefreshIntervalMs));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Task resumeSignal;
                lock (_pauseSync)
                    resumeSignal = _resumeTcs.Task;

                await resumeSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                bool isPaused;
                lock (_pauseSync)
                    isPaused = _isPaused;
                if (isPaused)
                    continue;

                await RefreshWindowsAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown path: the view model was disposed, nothing to report.
        }
    }

    /// <summary>
    /// Stops periodic window-list polling until <see cref="ResumeRefresh"/> is called.
    /// </summary>
    public void PauseRefresh()
    {
        lock (_pauseSync)
        {
            if (_isPaused)
                return;

            _isPaused = true;
            _resumeTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Resumes periodic window-list polling and requests an immediate refresh.
    /// </summary>
    public void ResumeRefresh()
    {
        TaskCompletionSource? resumeSignal = null;
        lock (_pauseSync)
        {
            if (!_isPaused)
                return;

            _isPaused = false;
            resumeSignal = _resumeTcs;
        }

        resumeSignal?.TrySetResult();
        _ = FetchWindowsWithFiltersAsync();
    }

    public bool IsRefreshPaused
    {
        get
        {
            lock (_pauseSync)
                return _isPaused;
        }
    }

    public void Dispose()
    {
        if (_cts.IsCancellationRequested)
            return;
        _cts.Cancel();
        lock (_pauseSync)
            _resumeTcs.TrySetResult();
        _cts.Dispose();
    }

    private static TaskCompletionSource CreateCompletedResumeSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }

    public async Task FetchWindowsWithFiltersAsync(CancellationToken cancellationToken = default)
    {
        await FetchAndApplyWindowsAsync(cancellationToken).ConfigureAwait(false);
    }

    public bool TrySelectWindowById(string windowId)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            return false;

        WindowConfig? matchingWindow = WindowsConfigs.FirstOrDefault(window =>
            string.Equals(window.WindowId, windowId, StringComparison.Ordinal)
        );
        if (matchingWindow is null)
            return false;

        SelectedWindow = matchingWindow;
        return true;
    }

    public PinWindowResult TryPinWindow(string windowId)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            return PinWindowResult.Ignored;

        ClosedFloatingPreviewIds.Remove(windowId);

        bool alreadyListed = WindowsConfigs.Any(window =>
            string.Equals(window.WindowId, windowId, StringComparison.Ordinal)
        );
        if (alreadyListed)
        {
            PinnedWindowIds.Add(windowId);
            return PinWindowResult.AlreadyOpen;
        }

        if (!PinnedWindowIds.Add(windowId))
            return PinWindowResult.Ignored;

        _ = FetchWindowsWithFiltersAsync();
        return PinWindowResult.Added;
    }

    /// <summary>
    /// Clears session close-suppressions so whitelist/prefix matching can show previews again.
    /// </summary>
    public void ClearClosedFloatingPreviews()
    {
        if (ClosedFloatingPreviewIds.Count == 0)
            return;

        ClosedFloatingPreviewIds.Clear();
        _ = FetchWindowsWithFiltersAsync();
    }

    /// <summary>
    /// Closes one floating preview for this session without changing the main window.
    /// </summary>
    public bool TryCloseFloatingPreview(string windowId)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            return false;

        PinnedWindowIds.Remove(windowId);
        ClosedFloatingPreviewIds.Add(windowId);

        WindowConfig? existing = WindowsConfigs.FirstOrDefault(window =>
            string.Equals(window.WindowId, windowId, StringComparison.Ordinal)
        );
        if (existing is null)
            return false;

        WindowsConfigs.Remove(existing);
        if (
            SelectedWindow is not null
            && string.Equals(SelectedWindow.WindowId, windowId, StringComparison.Ordinal)
        )
        {
            SelectedWindow = null;
        }

        return true;
    }

    private void ApplyWindowsWithFilters(IReadOnlyCollection<WindowConfig> fetchedWindows)
    {
        WindowFilterSettings filterSettings = _windowFilterSettingsProvider.GetSettings();

        var fetchedIds = new HashSet<string>(StringComparer.Ordinal);
        var existingById = WindowsConfigs.ToDictionary(
            config => config.WindowId,
            StringComparer.Ordinal
        );

        // Apply whitelist/blacklist rules while iterating fetched windows.
        foreach (WindowConfig fetchedWindow in fetchedWindows)
        {
            fetchedIds.Add(fetchedWindow.WindowId);

            bool isPinned = PinnedWindowIds.Contains(fetchedWindow.WindowId);
            bool isClosedSuppressed =
                !isPinned && ClosedFloatingPreviewIds.Contains(fetchedWindow.WindowId);
            bool isOnBlacklist =
                filterSettings.BlacklistPrefixes.Contains(fetchedWindow.WindowTitle)
                || TempWindowIdsBlacklist.Contains(fetchedWindow.WindowId)
                || isClosedSuppressed;
            bool isOnWhiteList =
                isPinned
                || filterSettings.WhitelistPrefixes.Any(prefix =>
                    fetchedWindow.WindowTitle.Contains(prefix, StringComparison.OrdinalIgnoreCase)
                );
            bool isOnWindowsList = existingById.TryGetValue(
                fetchedWindow.WindowId,
                out WindowConfig? existingConfig
            );

            if (isOnWindowsList && (isOnBlacklist || !isOnWhiteList))
            {
                WindowsConfigs.Remove(existingConfig!);
                existingById.Remove(fetchedWindow.WindowId);
            }
            else if (!isOnBlacklist && !isOnWindowsList && isOnWhiteList)
            {
                WindowsConfigs.Add(fetchedWindow);
                existingById[fetchedWindow.WindowId] = fetchedWindow;
            }
            else if (!isOnBlacklist && isOnWindowsList && isOnWhiteList)
            {
                if (existingConfig!.WindowTitle != fetchedWindow.WindowTitle)
                {
                    existingConfig.WindowTitle = fetchedWindow.WindowTitle;
                }
            }
        }

        // Remove stale windows that are no longer present in accessor output.
        for (int i = WindowsConfigs.Count - 1; i >= 0; i--)
        {
            WindowConfig window = WindowsConfigs[i];
            if (fetchedIds.Contains(window.WindowId))
                continue;

            PinnedWindowIds.Remove(window.WindowId);
            ClosedFloatingPreviewIds.Remove(window.WindowId);
            WindowsConfigs.RemoveAt(i);
        }

        PinnedWindowIds.RemoveWhere(pinnedWindowId => !fetchedIds.Contains(pinnedWindowId));
        ClosedFloatingPreviewIds.RemoveWhere(closedWindowId => !fetchedIds.Contains(closedWindowId));

        if (SelectedWindow is null)
            return;

        WindowConfig? selectedWindow = WindowsConfigs.FirstOrDefault(window =>
            string.Equals(window.WindowId, SelectedWindow.WindowId, StringComparison.Ordinal)
        );
        if (selectedWindow is not null)
        {
            SelectedWindow = selectedWindow;
            return;
        }

        SelectedWindow = null;
    }

    private async Task RefreshWindowsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await FetchAndApplyWindowsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown path: the view model was disposed, nothing to report.
        }
        catch (Exception exception)
        {
            // Keep the periodic refresh alive: the next tick retries with a fresh snapshot.
            _logger.Error(exception, "Window list refresh failed");
        }
    }

    private async Task FetchAndApplyWindowsAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyCollection<WindowConfig>? fetchedWindows = await _windowSnapshotProvider
                .TryGetWindowsAsync(cancellationToken)
                .ConfigureAwait(false);
            // An unreadable window list is not an empty desktop: keep the current windows and
            // their previews until the next successful snapshot.
            if (fetchedWindows is null)
                return;
            await _dispatcher.InvokeAsync(() => ApplyWindowsWithFilters(fetchedWindows));
        }
        finally
        {
            _refreshGate.Release();
        }
    }
}
