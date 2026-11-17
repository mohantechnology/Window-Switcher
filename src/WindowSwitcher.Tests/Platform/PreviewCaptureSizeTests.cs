using WindowSwitcher.Lib.Data.Platform;
using Xunit;

namespace WindowSwitcher.Tests.Platform;

public sealed class PreviewCaptureSizeTests
{
    [Theory]
    [InlineData(300, 200, 1920, 1280)]
    [InlineData(300, 169, 1920, 1082)]
    [InlineData(1920, 1080, 1920, 1080)]
    [InlineData(2560, 1440, 2560, 1440)]
    [InlineData(2000, 1000, 2160, 1080)]
    public void AtLeastFullHd_PreservesAspectAndMeets1080p(
        int width,
        int height,
        int expectedWidth,
        int expectedHeight
    )
    {
        (int nextWidth, int nextHeight) = PreviewCaptureSize.AtLeastFullHd(width, height);

        Assert.Equal(expectedWidth, nextWidth);
        Assert.Equal(expectedHeight, nextHeight);
    }
}
