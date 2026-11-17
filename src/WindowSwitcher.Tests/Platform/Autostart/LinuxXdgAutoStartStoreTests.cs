using WindowSwitcher.Lib.Data.Platform.Autostart;
using Xunit;

namespace WindowSwitcher.Tests.Platform.Autostart;

public sealed class LinuxXdgAutoStartStoreTests
{
    [Fact]
    public void WriteCommand_CreatesDesktopEntryWithExecLine()
    {
        string desktopFilePath = Path.Combine(Path.GetTempPath(), "autostart", "windowswitcher.desktop");
        string expectedDirectory = Path.GetDirectoryName(desktopFilePath)!;
        string? writtenPath = null;
        string? writtenContents = null;
        string? createdDirectory = null;
        var sut = new LinuxXdgAutoStartStore(
            desktopFilePath: desktopFilePath,
            readAllText: _ => writtenContents ?? string.Empty,
            writeAllText: (path, contents) =>
            {
                writtenPath = path;
                writtenContents = contents;
            },
            deleteFile: _ => { },
            fileExists: _ => writtenContents is not null,
            createDirectory: path => createdDirectory = path
        );

        sut.WriteCommand("\"/opt/WindowSwitcher/WindowSwitcher\" --background");

        Assert.Equal(expectedDirectory, createdDirectory);
        Assert.Equal(desktopFilePath, writtenPath);
        Assert.Contains("Exec=\"/opt/WindowSwitcher/WindowSwitcher\" --background", writtenContents);
        Assert.True(sut.Exists());
        Assert.Equal(
            "\"/opt/WindowSwitcher/WindowSwitcher\" --background",
            sut.ReadCommand()
        );
    }

    [Fact]
    public void Delete_RemovesDesktopEntryWhenPresent()
    {
        bool deleted = false;
        bool exists = true;
        var sut = new LinuxXdgAutoStartStore(
            desktopFilePath: "/tmp/autostart/windowswitcher.desktop",
            readAllText: _ => "Exec=test",
            writeAllText: (_, _) => { },
            deleteFile: _ =>
            {
                deleted = true;
                exists = false;
            },
            fileExists: _ => exists,
            createDirectory: _ => { }
        );

        sut.Delete();

        Assert.True(deleted);
        Assert.False(sut.Exists());
    }
}
