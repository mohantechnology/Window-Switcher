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
