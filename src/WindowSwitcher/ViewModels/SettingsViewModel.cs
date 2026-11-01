using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using WindowSwitcher.Lib.Data.Platform.Autostart.Abstractions;
using WindowSwitcher.Lib.Models;
using WindowSwitcher.ViewModels.Abstractions;

namespace WindowSwitcher.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<SettingsViewModel>();

    private readonly ISettingsRepository _settingsRepository;
    private readonly IAutoStartService _autoStartService;
    private readonly Action _applyAction;
    private readonly Action<string> _applyPreviewHighlightColorAction;
    private bool _autoStartInBackground;
    private bool _isUpdatingAutoStart;
    private string _autoStartError = string.Empty;

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        IAutoStartService autoStartService,
        Action applyAction,
        Action<string> applyPreviewHighlightColorAction
    )
    {
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(autoStartService);
        ArgumentNullException.ThrowIfNull(applyAction);
        ArgumentNullException.ThrowIfNull(applyPreviewHighlightColorAction);

        _settingsRepository = settingsRepository;
        _autoStartService = autoStartService;
        _applyAction = applyAction;
        _applyPreviewHighlightColorAction = applyPreviewHighlightColorAction;
        _autoStartInBackground = _settingsRepository.Read(config => config.AutoStartInBackground);
    }

    public bool EnablePreviews
    {
        get => ReadSetting(config => config.EnablePreviews);
        set =>
            UpdateSetting(
                nameof(EnablePreviews),
                value,
                config => config.EnablePreviews,
                (config, currentValue) => config.EnablePreviews = currentValue,
                applyToWindows: true
            );
    }

    public bool EnablePinFocusedWindow
    {
        get => ReadSetting(config => config.EnablePinFocusedWindow);
        set =>
            UpdateSetting(
                nameof(EnablePinFocusedWindow),
                value,
                config => config.EnablePinFocusedWindow,
                (config, currentValue) => config.EnablePinFocusedWindow = currentValue
            );
    }

    public bool StartMinimized
    {
        get => ReadSetting(config => config.StartMinimized);
        set =>
            UpdateSetting(
                nameof(StartMinimized),
                value,
                config => config.StartMinimized,
                (config, currentValue) => config.StartMinimized = currentValue
            );
    }

    public bool AutoStartInBackground
    {
        get => _autoStartInBackground;
        set
        {
            if (_isUpdatingAutoStart || _autoStartInBackground == value)
                return;

            _ = SetAutoStartInBackgroundAsync(value);
        }
    }

    public string AutoStartError
    {
        get => _autoStartError;
        private set
        {
            if (SetProperty(ref _autoStartError, value ?? string.Empty))
                OnPropertyChanged(nameof(HasAutoStartError));
        }
    }

    public bool HasAutoStartError => !string.IsNullOrWhiteSpace(AutoStartError);

    public bool ResizeWindows
    {
        get => ReadSetting(config => config.ResizeWindows);
        set =>
            UpdateSetting(
                nameof(ResizeWindows),
                value,
                config => config.ResizeWindows,
                (config, currentValue) => config.ResizeWindows = currentValue,
                applyToWindows: true
            );
    }

    public bool CanEditResizeWindows => !UseFixedWindowSize;

    public bool MoveWindows
    {
        get => ReadSetting(config => config.MoveWindows);
        set =>
            UpdateSetting(
                nameof(MoveWindows),
                value,
                config => config.MoveWindows,
                (config, currentValue) => config.MoveWindows = currentValue
            );
    }

    public bool FocusOnHover
    {
        get => ReadSetting(config => config.FocusOnHover);
        set =>
            UpdateSetting(
                nameof(FocusOnHover),
                value,
                config => config.FocusOnHover,
                (config, currentValue) => config.FocusOnHover = currentValue
            );
    }

    public bool UseFixedWindowSize
    {
        get => ReadSetting(config => config.UseFixedWindowSize);
        set
        {
            bool updated = UpdateSetting(
                nameof(UseFixedWindowSize),
                value,
                config => config.UseFixedWindowSize,
                (config, currentValue) => config.UseFixedWindowSize = currentValue,
                applyToWindows: true
            );
            if (updated)
                OnPropertyChanged(nameof(CanEditResizeWindows));
        }
    }

    public int WindowWidth
    {
        get => ReadSetting(config => config.WindowWidth);
        set =>
            UpdateSetting(
                nameof(WindowWidth),
                value,
                config => config.WindowWidth,
                (config, currentValue) => config.WindowWidth = currentValue,
                applyToWindows: true
            );
    }

    public int WindowHeight
    {
        get => ReadSetting(config => config.WindowHeight);
        set =>
            UpdateSetting(
                nameof(WindowHeight),
                value,
                config => config.WindowHeight,
                (config, currentValue) => config.WindowHeight = currentValue,
                applyToWindows: true
            );
    }

    public string PreviewHighlightColor
    {
        get
        {
            string value = _settingsRepository.Read(config => config.PreviewHighlightColor);
            return string.IsNullOrWhiteSpace(value) ? "#E3008C" : value;
        }
        set
        {
            string configValue = string.IsNullOrWhiteSpace(value) ? "#E3008C" : value.Trim();

            bool updated = false;
            _settingsRepository.Update(config =>
            {
                if (
                    string.Equals(
                        config.PreviewHighlightColor,
                        configValue,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return;
                config.PreviewHighlightColor = configValue;
                updated = true;
            });
            if (updated)
            {
                _applyPreviewHighlightColorAction(configValue);
                _applyAction();
                OnPropertyChanged();
            }
        }
    }

    private async Task SetAutoStartInBackgroundAsync(bool value)
    {
        bool previous = _autoStartInBackground;
        _isUpdatingAutoStart = true;
        _autoStartInBackground = value;
        OnPropertyChanged(nameof(AutoStartInBackground));
        AutoStartError = string.Empty;

        try
        {
            // Stay on the UI synchronization context so bindable properties update safely.
            await _autoStartService.SetEnabledAsync(value);
            _settingsRepository.Update(config => config.AutoStartInBackground = value);
        }
        catch (Exception exception)
        {
            Logger.Warning(exception, "Auto-start preference could not be applied");
            _autoStartInBackground = previous;
            OnPropertyChanged(nameof(AutoStartInBackground));
            AutoStartError = "Could not update auto-start. Check permissions and try again.";
        }
        finally
        {
            _isUpdatingAutoStart = false;
        }
    }

    private T ReadSetting<T>(Func<ConfigFile, T> selector)
    {
        return _settingsRepository.Read(selector);
    }

    private bool UpdateSetting<T>(
        string propertyName,
        T newValue,
        Func<ConfigFile, T> selector,
        Action<ConfigFile, T> updater,
        bool applyToWindows = false
    )
    {
        bool updated = false;
        _settingsRepository.Update(config =>
        {
            if (EqualityComparer<T>.Default.Equals(selector(config), newValue))
                return;

            updater(config, newValue);
            updated = true;
        });

        if (updated)
        {
            OnPropertyChanged(propertyName);
            if (applyToWindows)
                _applyAction();
        }

        return updated;
    }
}
