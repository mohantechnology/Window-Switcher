using WindowSwitcher.Lib.Data.Platform;
using Xunit;

namespace WindowSwitcher.Tests.Platform;

public sealed class LinkedPreviewSizeTests
{
    [Fact]
    public void FitWidth_SetsHeightFromTheSourceAspect()
    {
        (int width, int height) = LinkedPreviewSize.FitWidth(300, 1920 / 1080d, reservedHeight: 26);

        Assert.Equal(300, width);
        Assert.Equal(195, height);
    }

    [Fact]
    public void Preserve_UsesHeightWhenTheVerticalEdgeDrivesTheResize()
    {
        (int width, int height) = LinkedPreviewSize.Preserve(
            width: 100,
            height: 200,
            aspectWidthOverHeight: 2,
            widthDrives: false
        );

        Assert.Equal(200, height);
        Assert.Equal(400, width);
    }

    [Fact]
    public void Scale_KeepsTheLinkedAspect()
    {
        (int width, int height) = LinkedPreviewSize.Scale(
            width: 300,
            height: 195,
            increase: true,
            aspectWidthOverHeight: 1920 / 1080d,
            reservedHeight: 26
        );

        Assert.Equal(330, width);
        Assert.Equal(212, height);
    }
}
