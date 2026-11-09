using WindowSwitcher.Lib.Models;
using Xunit;

namespace WindowSwitcher.Tests.Models;

public sealed class FloatingPreviewPreferenceTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void IsEnabled_UsesWindowOverride(
        bool windowOverride,
        bool globalEnabled,
        bool expected
    )
    {
        Assert.Equal(
            expected,
            FloatingPreviewPreference.IsEnabled(windowOverride, globalEnabled)
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_FollowsGlobalWhenWindowHasNoOverride(bool globalEnabled)
    {
        Assert.Equal(globalEnabled, FloatingPreviewPreference.IsEnabled(null, globalEnabled));
    }

    [Fact]
    public void TitleBandHeight_ReservesTwentySixPixelsAtTheDefaultFontSize()
    {
        Assert.Equal(10, FloatingWindowLabelLayout.DefaultFontSize);
        Assert.Equal(26, FloatingWindowLabelLayout.TitleBandHeight(10));
    }

    [Theory]
    [InlineData(8, 24)]
    [InlineData(32, 48)]
    [InlineData(1, 24)]
    [InlineData(100, 48)]
    public void TitleBandHeight_ClampsFontSizeThenAddsSixteen(int fontSize, int expectedHeight)
    {
        Assert.Equal(expectedHeight, FloatingWindowLabelLayout.TitleBandHeight(fontSize));
    }
}
