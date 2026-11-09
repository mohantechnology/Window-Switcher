using System;
using WindowSwitcher.Lib.Data;
using WindowSwitcher.Lib.Models;

namespace WindowSwitcher.Windows.Services;

internal interface IFloatingWindowSettingsService
{
    WindowConfig? GetPersistedConfig(WindowConfig runtimeConfig);

    FloatingWindowBehaviorSettings GetBehaviorSettings();

    FloatingWindowAppearanceSettings GetAppearanceSettings();

    void Save(WindowConfig windowConfig);
}

internal sealed record FloatingWindowBehaviorSettings(
    bool MoveWindows,
    bool FocusOnHover,
    bool ChangeSizeOnHover,
    bool ChangeOpacityOnHover
);

internal sealed record FloatingWindowAppearanceSettings(
    bool ResizeWindows,
    bool UseFixedWindowSize,
    bool UseDefaultWindowSize,
    bool LinkPreviewAspectRatio,
    int WindowWidth,
    int WindowHeight,
    string PreviewHighlightColor,
    double DefaultPreviewOpacity,
    int PreviewFrameRate,
    int FloatingWindowLabelFontSize
);

internal sealed class ConfigFileFloatingWindowSettingsService : IFloatingWindowSettingsService
{
    public WindowConfig? GetPersistedConfig(WindowConfig runtimeConfig)
    {
        ArgumentNullException.ThrowIfNull(runtimeConfig);

        return ConfigFileAccessor.GetInstance().GetFloatingWindowConfig(runtimeConfig);
    }

    public FloatingWindowBehaviorSettings GetBehaviorSettings()
    {
        return ConfigFileAccessor
            .GetInstance()
            .ReadConfig(config => new FloatingWindowBehaviorSettings(
                config.MoveWindows,
                config.FocusOnHover,
                config.ChangeSizeOnHover,
                config.ChangeOpacityOnHover
            ));
    }

    public FloatingWindowAppearanceSettings GetAppearanceSettings()
    {
        return ConfigFileAccessor
            .GetInstance()
            .ReadConfig(config => new FloatingWindowAppearanceSettings(
                config.ResizeWindows,
                config.UseFixedWindowSize,
                config.UseDefaultWindowSize,
                config.LinkPreviewAspectRatio,
                config.WindowWidth,
                config.WindowHeight,
                config.PreviewHighlightColor,
                config.DefaultPreviewOpacity,
                config.PreviewFrameRate,
                config.FloatingWindowLabelFontSize
            ));
    }

    public void Save(WindowConfig windowConfig)
    {
        ArgumentNullException.ThrowIfNull(windowConfig);

        ConfigFileAccessor.GetInstance().SaveFloatingWindowSettings(windowConfig);
    }
}
