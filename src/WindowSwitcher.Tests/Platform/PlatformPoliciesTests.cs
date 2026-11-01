using WindowSwitcher.Lib.Data.Platform.Policies;
using WindowSwitcher.Lib.Models;
using Xunit;

namespace WindowSwitcher.Tests.Platform;

public sealed class PlatformPoliciesTests
{
    [Fact]
    public void LinuxAndWindowsPreviewPolicies_ExposeExpectedPreviewFlags()
    {
        var linuxPolicy = new LinuxFloatingPreviewPolicy();
        Assert.False(linuxPolicy.UseNativeThumbnailPreview);
        Assert.True(linuxPolicy.ShowScreenshotControl);
        Assert.True(linuxPolicy.RefreshScreenshotWhenDeselected);
        Assert.False(linuxPolicy.UseWindowBorder);

        var windowsPolicy = new WindowsFloatingPreviewPolicy();
        Assert.True(windowsPolicy.UseNativeThumbnailPreview);
        Assert.False(windowsPolicy.ShowScreenshotControl);
        Assert.False(windowsPolicy.RefreshScreenshotWhenDeselected);
        Assert.True(windowsPolicy.UseWindowBorder);
    }

    [Fact]
    public void NoOpNativeThumbnailRenderer_DoesNotRegisterThumbnail()
    {
        var renderer = new NoOpNativeThumbnailRenderer();

        bool registered = renderer.TryRegister(
            destinationWindowHandle: 1,
            sourceWindowHandle: 2,
            destinationBounds: new NativeThumbnailBounds(0, 0, 100, 100),
            opacity: 204,
            out nint thumbnailHandle
        );

        Assert.False(registered);
        Assert.Equal(0, thumbnailHandle);
        Assert.False(renderer.TryUpdateOpacity(thumbnailHandle: 1, opacity: 128));
    }

    [Theory]
    [InlineData(0.8, 204)]
    [InlineData(1.0, 255)]
    [InlineData(0.2, 51)]
    public void ToByteOpacity_MapsNormalizedOpacity(double opacity, byte expected)
    {
        Assert.Equal(expected, WindowSwitcher.Lib.Data.Platform.FloatingPreviewHoverAdjustment.ToByteOpacity(opacity));
    }
}
