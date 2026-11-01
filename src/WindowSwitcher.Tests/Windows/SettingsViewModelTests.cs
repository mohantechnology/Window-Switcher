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

    private static SettingsViewModel CreateSut(
        FakeSettingsRepository? repository = null,
        Action<string>? applyColor = null,
        Action? applyAction = null
    )
    {
        return new SettingsViewModel(
            repository ?? new FakeSettingsRepository(),
            applyAction ?? (() => { }),
            applyColor ?? (_ => { })
        );
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
}
