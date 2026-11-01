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
        IAutoStartService? autoStartService = null
    )
    {
        return new SettingsViewModel(
            repository ?? new FakeSettingsRepository(),
            autoStartService ?? new FakeAutoStartService(),
            applyAction ?? (() => { }),
            applyColor ?? (_ => { })
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
