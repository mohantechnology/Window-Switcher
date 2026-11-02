using System;
using Avalonia.Controls;
using Avalonia.Media;
using WindowSwitcher.Hosting;
using WindowSwitcher.Lib.Data.Platform.Autostart.Abstractions;
using WindowSwitcher.Theming;
using WindowSwitcher.ViewModels;
using WindowSwitcher.Windows.Services;

namespace WindowSwitcher.Windows;

public partial class SettingsWindow : Window
{
    private readonly UtilityWindowService _windowLifecycle = new();
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(Action applyAction, Action<double> applyDefaultPreviewOpacityAction)
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel(
            new ConfigFileSettingsRepository(),
            AppServiceProvider.GetRequiredService<IAutoStartService>(),
            applyAction,
            ApplyPreviewHighlightColor,
            applyDefaultPreviewOpacityAction
        );
        DataContext = _viewModel;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _windowLifecycle.HandleClosing(this, e, hideWhenCanceled: true, hideWhenAllowed: true);
    }

    private static void ApplyPreviewHighlightColor(string colorValue)
    {
        if (Color.TryParse(colorValue, out Color color))
            AccentColorApplier.Apply(color);
    }
}
