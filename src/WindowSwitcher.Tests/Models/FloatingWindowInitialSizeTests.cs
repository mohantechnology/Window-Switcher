using WindowSwitcher.Lib.Models;
using Xunit;

namespace WindowSwitcher.Tests.Models;

public sealed class FloatingWindowInitialSizeTests
{
    [Theory]
    [InlineData(false, 100, 80, false, false, 640, 480, 300, 200)]
    [InlineData(false, 100, 80, true, false, 640, 480, 640, 480)]
    [InlineData(false, 100, 80, false, true, 640, 480, 640, 480)]
    [InlineData(false, 100, 80, true, true, 640, 480, 640, 480)]
    [InlineData(true, 100, 80, true, false, 640, 480, 100, 80)]
    [InlineData(true, 100, 80, false, false, 640, 480, 100, 80)]
    [InlineData(true, 100, 80, false, true, 640, 480, 640, 480)]
    [InlineData(true, 100, 80, true, true, 640, 480, 640, 480)]
    public void Resolve_PrefersFixedSizeThenSavedLayoutThenDefault(
        bool hasPersistedLayout,
        int persistedWidth,
        int persistedHeight,
        bool useDefaultWindowSize,
        bool useFixedWindowSize,
        int configuredWidth,
        int configuredHeight,
        int expectedWidth,
        int expectedHeight
    )
    {
        (int width, int height) = FloatingWindowInitialSize.Resolve(
            hasPersistedLayout,
            persistedWidth,
            persistedHeight,
            useDefaultWindowSize,
            useFixedWindowSize,
            configuredWidth,
            configuredHeight
        );

        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }
}
