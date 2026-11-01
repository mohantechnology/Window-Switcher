using System.Runtime.Versioning;
using Microsoft.Win32;
using WindowSwitcher.Lib.Data.Platform.Autostart;
using Xunit;

namespace WindowSwitcher.Tests.Platform.Autostart;

public sealed class WindowsRunKeyAutoStartStoreTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public void WriteCommand_StoresValueUnderRunKey()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using RegistryKey runKey = root.CreateSubKey(
            @"Software\WindowSwitcherTests\Run",
            writable: true
        );
        try
        {
            var sut = new WindowsRunKeyAutoStartStore(
                openWritableRunKey: () =>
                    root.CreateSubKey(@"Software\WindowSwitcherTests\Run", writable: true),
                openReadableRunKey: () =>
                    root.OpenSubKey(@"Software\WindowSwitcherTests\Run", writable: false)
            );

            sut.WriteCommand("\"C:\\Apps\\WindowSwitcher.exe\" --background");

            Assert.True(sut.Exists());
            Assert.Equal(
                "\"C:\\Apps\\WindowSwitcher.exe\" --background",
                sut.ReadCommand()
            );

            sut.Delete();
            Assert.False(sut.Exists());
        }
        finally
        {
            root.DeleteSubKeyTree(@"Software\WindowSwitcherTests", throwOnMissingSubKey: false);
        }
    }
}
