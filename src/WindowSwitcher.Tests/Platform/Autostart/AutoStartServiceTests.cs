using WindowSwitcher.Lib.Data.Platform.Autostart;
using WindowSwitcher.Lib.Data.Platform.Autostart.Abstractions;
using Xunit;

namespace WindowSwitcher.Tests.Platform.Autostart;

public sealed class AutoStartServiceTests
{
    [Fact]
    public void AutoStartCommandBuilder_QuotesExecutableAndAppendsBackgroundArgument()
    {
        string command = AutoStartCommandBuilder.Build(@"C:\Apps\WindowSwitcher.exe");

        Assert.Equal("\"C:\\Apps\\WindowSwitcher.exe\" --background", command);
    }

    [Fact]
    public async Task SetEnabledAsync_WritesCommandWhenEnabled()
    {
        var store = new FakeAutoStartRegistrationStore();
        var sut = new AutoStartService(store, () => @"C:\Apps\WindowSwitcher.exe");

        await sut.SetEnabledAsync(true);

        Assert.True(await sut.IsEnabledAsync());
        Assert.Equal(
            "\"C:\\Apps\\WindowSwitcher.exe\" --background",
            store.Command
        );
    }

    [Fact]
    public async Task SetEnabledAsync_DeletesRegistrationWhenDisabled()
    {
        var store = new FakeAutoStartRegistrationStore
        {
            Command = "\"C:\\Apps\\WindowSwitcher.exe\" --background",
        };
        var sut = new AutoStartService(store, () => @"C:\Apps\WindowSwitcher.exe");

        await sut.SetEnabledAsync(false);

        Assert.False(await sut.IsEnabledAsync());
        Assert.Null(store.Command);
    }

    [Fact]
    public async Task SetEnabledAsync_ThrowsWhenExecutablePathIsMissing()
    {
        var store = new FakeAutoStartRegistrationStore();
        var sut = new AutoStartService(store, () => string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SetEnabledAsync(true));
    }

    private sealed class FakeAutoStartRegistrationStore : IAutoStartRegistrationStore
    {
        public string? Command { get; set; }

        public bool Exists() => !string.IsNullOrWhiteSpace(Command);

        public string? ReadCommand() => Command;

        public void WriteCommand(string command) => Command = command;

        public void Delete() => Command = null;
    }
}
