using WindowSwitcher.Lib.Data.Platform.Autostart.Abstractions;
using WindowSwitcher.Lib.Models;
using WindowSwitcher.ViewModels;
using WindowSwitcher.ViewModels.Abstractions;
using Xunit;

namespace WindowSwitcher.Tests.Windows;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void PreviewHighlightColor_UpdatesRepositoryAndInvokesApplyCallback()
    {
        var repository = new FakeSettingsRepository();
        string appliedColor = string.Empty;
        var sut = CreateSut(repository, color => appliedColor = color);

        sut.PreviewHighlightColor = "  #112233  ";

        Assert.Equal("#112233", repository.Config.PreviewHighlightColor);
        Assert.Equal("#112233", appliedColor);
    }

    [Fact]
    public void EnablePreviews_AppliesImmediatelyAndInvokesApplyAction()
    {
        var repository = new FakeSettingsRepository();
        repository.Config.EnablePreviews = false;
        int applyActionInvocationCount = 0;
        var sut = CreateSut(repository, _ => { }, () => applyActionInvocationCount++);

        Assert.False(repository.Config.EnablePreviews);

        sut.EnablePreviews = true;

        Assert.True(repository.Config.EnablePreviews);
        Assert.Equal(1, applyActionInvocationCount);
    }

    [Fact]
    public void EnablePreviews_ClearsPerWindowOverridesAndAppliesOnce()
    {
        var repository = new FakeSettingsRepository();
        repository.Config.EnablePreviews = true;
        repository.Config.FloatingWindowsConfig.Add(
            new WindowConfig { WindowTitle = "Editor", LivePreviewOverride = false }
        );
        repository.Config.FloatingWindowsConfig.Add(
            new WindowConfig { WindowTitle = "Terminal", LivePreviewOverride = true }
        );
        int applyActionInvocationCount = 0;
        var sut = CreateSut(repository, applyAction: () => applyActionInvocationCount++);

        sut.EnablePreviews = false;

        Assert.False(repository.Config.EnablePreviews);
        Assert.All(
            repository.Config.FloatingWindowsConfig,
            windowConfig => Assert.Null(windowConfig!.LivePreviewOverride)
        );
        Assert.Equal(1, applyActionInvocationCount);
    }

    [Fact]
    public void EnablePreviews_SameValue_LeavesOverridesAndDoesNotApply()
    {
        var repository = new FakeSettingsRepository();
        repository.Config.EnablePreviews = true;
        repository.Config.FloatingWindowsConfig.Add(
            new WindowConfig { WindowTitle = "Editor", LivePreviewOverride = false }
        );
        int applyActionInvocationCount = 0;
        var sut = CreateSut(repository, applyAction: () => applyActionInvocationCount++);

        sut.EnablePreviews = true;

        Assert.False(repository.Config.FloatingWindowsConfig[0]!.LivePreviewOverride);
        Assert.Equal(0, applyActionInvocationCount);
    }

    [Fact]
    public void EnablePinFocusedWindow_DefaultsToTrueAndPersistsChanges()
    {
        var repository = new FakeSettingsRepository();
        var sut = CreateSut(repository);

        Assert.True(sut.EnablePinFocusedWindow);

        sut.EnablePinFocusedWindow = false;

        Assert.False(repository.Config.EnablePinFocusedWindow);
        Assert.False(sut.EnablePinFocusedWindow);
    }

    [Fact]
    public void ChangeSizeOnHover_DefaultsToTrueAndPersistsChanges()
    {
        var repository = new FakeSettingsRepository();
        var sut = CreateSut(repository);

        Assert.True(sut.ChangeSizeOnHover);

        sut.ChangeSizeOnHover = false;

        Assert.False(repository.Config.ChangeSizeOnHover);
        Assert.False(sut.ChangeSizeOnHover);
    }

    [Fact]
    public void ChangeOpacityOnHover_DefaultsToTrueAndPersistsChanges()
    {
        var repository = new FakeSettingsRepository();
        var sut = CreateSut(repository);

        Assert.True(sut.ChangeOpacityOnHover);

        sut.ChangeOpacityOnHover = false;

        Assert.False(repository.Config.ChangeOpacityOnHover);
        Assert.False(sut.ChangeOpacityOnHover);
    }

    [Fact]
    public void LinkPreviewAspectRatio_DefaultsToTrueAndPersists()
    {
        var repository = new FakeSettingsRepository();
        var sut = CreateSut(repository);

        Assert.True(sut.LinkPreviewAspectRatio);

        sut.LinkPreviewAspectRatio = false;

        Assert.False(repository.Config.LinkPreviewAspectRatio);
        Assert.False(sut.LinkPreviewAspectRatio);
    }

    [Fact]
    public void UseDefaultWindowSize_DefaultsToTrueAndPersistsWithoutResizingOpenWindows()
    {
        var repository = new FakeSettingsRepository();
        int applyActionInvocationCount = 0;
        var sut = CreateSut(repository, applyAction: () => applyActionInvocationCount++);

        Assert.True(sut.UseDefaultWindowSize);
        Assert.True(sut.CanEditWindowSize);

        sut.UseDefaultWindowSize = false;

        Assert.False(repository.Config.UseDefaultWindowSize);
        Assert.False(sut.UseDefaultWindowSize);
        Assert.False(sut.CanEditWindowSize);
        Assert.Equal(0, applyActionInvocationCount);
    }

    [Fact]
    public void WindowSize_AppliesToOpenWindowsOnlyWhenFixedSizeIsOn()
    {
        var repository = new FakeSettingsRepository();
        repository.Config.UseFixedWindowSize = false;
        int applyActionInvocationCount = 0;
        var sut = CreateSut(repository, applyAction: () => applyActionInvocationCount++);

        sut.WindowWidth = 640;

        Assert.Equal(640, repository.Config.WindowWidth);
        Assert.Equal(0, applyActionInvocationCount);

        sut.UseFixedWindowSize = true;
        sut.WindowHeight = 360;

        Assert.Equal(360, repository.Config.WindowHeight);
        Assert.Equal(2, applyActionInvocationCount);
    }

    [Fact]
    public void CanEditWindowSize_IsEnabledWhenEitherSizeOptionIsOn()
    {
        var repository = new FakeSettingsRepository();
        repository.Config.UseDefaultWindowSize = false;
        repository.Config.UseFixedWindowSize = false;
        var sut = CreateSut(repository);

        Assert.False(sut.CanEditWindowSize);

        sut.UseFixedWindowSize = true;

        Assert.True(sut.CanEditWindowSize);
    }

    [Fact]
    public void DefaultPreviewOpacity_DefaultsToFullAndAppliesToOpenWindows()
    {
        var repository = new FakeSettingsRepository();
        double appliedOpacity = 0;
        var sut = CreateSut(repository, applyDefaultOpacity: opacity => appliedOpacity = opacity);

        Assert.Equal(1.0, sut.DefaultPreviewOpacity);

        sut.DefaultPreviewOpacity = 0.55;

        Assert.Equal(0.55, repository.Config.DefaultPreviewOpacity);
        Assert.Equal(0.55, appliedOpacity);
    }

    [Fact]
    public void DefaultPreviewOpacity_UpdatesPersistedFloatingWindowOpacity()
    {
        var repository = new FakeSettingsRepository();
        repository.Config.FloatingWindowsConfig.Add(
            new WindowConfig { WindowTitle = "Editor", PreviewOpacity = 0.8 }
        );
        var sut = CreateSut(repository);

        sut.DefaultPreviewOpacity = 1.0;

        Assert.Equal(1.0, repository.Config.FloatingWindowsConfig[0]!.PreviewOpacity);
    }

    [Fact]
    public void PreviewFrameRate_DefaultsToOneAndPersistsChanges()
    {
        var repository = new FakeSettingsRepository();
        int applyActionInvocationCount = 0;
        var sut = CreateSut(repository, applyAction: () => applyActionInvocationCount++);

        Assert.Equal(1, sut.PreviewFrameRate);

        sut.PreviewFrameRate = 5;

        Assert.Equal(5, repository.Config.PreviewFrameRate);
        Assert.Equal(1, applyActionInvocationCount);
    }

    [Fact]
    public void PreviewFrameRate_ClampsToSupportedRange()
    {
        var repository = new FakeSettingsRepository();
        var sut = CreateSut(repository);

        sut.PreviewFrameRate = 99;
        Assert.Equal(30, repository.Config.PreviewFrameRate);

        sut.PreviewFrameRate = -3;
        Assert.Equal(0, repository.Config.PreviewFrameRate);
    }

    [Fact]
    public void FloatingWindowLabelFontSize_DefaultsToTenAndPersistsChanges()
    {
        var repository = new FakeSettingsRepository();
        int applyActionInvocationCount = 0;
        var sut = CreateSut(repository, applyAction: () => applyActionInvocationCount++);

        Assert.Equal(10, sut.FloatingWindowLabelFontSize);

        sut.FloatingWindowLabelFontSize = 16;

        Assert.Equal(16, repository.Config.FloatingWindowLabelFontSize);
        Assert.Equal(1, applyActionInvocationCount);
    }

    [Fact]
    public void FloatingWindowLabelFontSize_ClampsToSupportedRange()
    {
        var repository = new FakeSettingsRepository();
        var sut = CreateSut(repository);

        sut.FloatingWindowLabelFontSize = 99;
        Assert.Equal(32, repository.Config.FloatingWindowLabelFontSize);

        sut.FloatingWindowLabelFontSize = 1;
        Assert.Equal(8, repository.Config.FloatingWindowLabelFontSize);
    }

    [Fact]
    public async Task AutoStartInBackground_PersistsAndAppliesWhenServiceSucceeds()
    {
        var repository = new FakeSettingsRepository();
        repository.Config.AutoStartInBackground = false;
        var autoStart = new FakeAutoStartService();
        var sut = CreateSut(repository, autoStartService: autoStart);

        Assert.False(sut.AutoStartInBackground);

        sut.AutoStartInBackground = true;
        await WaitForAsync(() => repository.Config.AutoStartInBackground);

        Assert.True(sut.AutoStartInBackground);
        Assert.True(autoStart.Enabled);
        Assert.False(sut.HasAutoStartError);
    }

    [Fact]
    public async Task AutoStartInBackground_RevertsAndSurfacesErrorWhenServiceFails()
    {
        var repository = new FakeSettingsRepository();
        repository.Config.AutoStartInBackground = true;
        var autoStart = new FakeAutoStartService
        {
            SetEnabledException = new InvalidOperationException("denied"),
        };
        var sut = CreateSut(repository, autoStartService: autoStart);

        sut.AutoStartInBackground = false;
        await WaitForAsync(() => sut.HasAutoStartError);

        Assert.True(sut.AutoStartInBackground);
        Assert.True(repository.Config.AutoStartInBackground);
        Assert.Contains("auto-start", sut.AutoStartError, StringComparison.OrdinalIgnoreCase);
    }

    private static SettingsViewModel CreateSut(
        FakeSettingsRepository? repository = null,
        Action<string>? applyColor = null,
        Action? applyAction = null,
        Action<double>? applyDefaultOpacity = null,
        IAutoStartService? autoStartService = null
    )
    {
        return new SettingsViewModel(
            repository ?? new FakeSettingsRepository(),
            autoStartService ?? new FakeAutoStartService(),
            applyAction ?? (() => { }),
            applyColor ?? (_ => { }),
            applyDefaultOpacity ?? (_ => { })
        );
    }

    private static async Task WaitForAsync(Func<bool> probe)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!probe())
            await Task.Delay(25, timeout.Token);
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        public ConfigFile Config { get; } = new();

        public T Read<T>(Func<ConfigFile, T> reader)
        {
            return reader(Config);
        }

        public void Update(Action<ConfigFile> update)
        {
            update(Config);
        }
    }

    private sealed class FakeAutoStartService : IAutoStartService
    {
        public bool Enabled { get; private set; }

        public Exception? SetEnabledException { get; set; }

        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Enabled);
        }

        public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            if (SetEnabledException is not null)
                return Task.FromException(SetEnabledException);

            Enabled = enabled;
            return Task.CompletedTask;
        }
    }
}
