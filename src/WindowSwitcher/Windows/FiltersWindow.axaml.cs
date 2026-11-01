using System;
using System.Collections.Generic;
using Avalonia.Controls;
using WindowSwitcher.Lib.Data;
using WindowSwitcher.ViewModels;
using WindowSwitcher.Windows.Services;

namespace WindowSwitcher.Windows;

public partial class FiltersWindow : Window
{
    private readonly UtilityWindowService _windowService = new();
    private readonly Action? _onWhitelistChanged;
    private FiltersViewModel ViewModel { get; }

    public FiltersWindow(
        List<string> whitelistPrefixes,
        List<string> blacklistPrefixes,
        Action? onWhitelistChanged = null
    )
    {
        InitializeComponent();

        _onWhitelistChanged = onWhitelistChanged;
        var whitelistViewModel = new PrefixListViewModel(
            new PrefixListService(whitelistPrefixes, StaticData.PrefixWindowType.whitelist),
            new AvaloniaViewModelDispatcher()
        );
        whitelistViewModel.PrefixesChanged += OnWhitelistPrefixesChanged;

        ViewModel = new FiltersViewModel(
            whitelistViewModel,
            new PrefixListViewModel(
                new PrefixListService(blacklistPrefixes, StaticData.PrefixWindowType.blacklist),
                new AvaloniaViewModelDispatcher()
            )
        );
        DataContext = ViewModel;

        Closing += OnClosing;
    }

    private void OnWhitelistPrefixesChanged(object? sender, EventArgs e)
    {
        _onWhitelistChanged?.Invoke();
    }

    public void ShowPrefixesTab()
    {
        ViewModel.SelectedTabIndex = 0;
        ShowWindow();
    }

    public void ShowBlacklistTab()
    {
        ViewModel.SelectedTabIndex = 1;
        ShowWindow();
    }

    public bool HasBlacklistPrefixStartingWith(string prefix)
    {
        return ViewModel.Blacklist.ContainsPrefixStartingWith(prefix);
    }

    public bool TryAddBlacklistPrefix(string prefix)
    {
        return ViewModel.Blacklist.TryAddPrefix(prefix);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _windowService.HandleClosing(this, e, hideWhenCanceled: true, hideWhenAllowed: true);
    }

    private void ShowWindow()
    {
        if (IsVisible)
        {
            Activate();
            return;
        }

        Show();
    }
}
