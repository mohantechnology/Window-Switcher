using WindowSwitcher.Lib.Data.Platform;
using WindowSwitcher.Lib.Data.Platform.Keybinds.Models;
using Xunit;

namespace WindowSwitcher.Tests.Platform;

public sealed class FloatingPreviewHoverAdjustmentTests
{
    [Theory]
    [InlineData(100, 80, true, 110, 88)]
    [InlineData(110, 88, false, 100, 80)]
    [InlineData(50, 50, false, 50, 50)]
    [InlineData(2000, 2000, true, 2000, 2000)]
    public void ScaleSize_ScalesAndClamps(
        int width,
        int height,
        bool increase,
        int expectedWidth,
        int expectedHeight
    )
    {
        (int nextWidth, int nextHeight) = FloatingPreviewHoverAdjustment.ScaleSize(
            width,
            height,
            increase
        );

        Assert.Equal(expectedWidth, nextWidth);
        Assert.Equal(expectedHeight, nextHeight);
    }

    [Theory]
    [InlineData(0.8, true, 0.85)]
    [InlineData(0.8, false, 0.75)]
    [InlineData(0.2, false, 0.2)]
    [InlineData(1.0, true, 1.0)]
    public void AdjustOpacity_StepsAndClamps(double opacity, bool increase, double expected)
    {
        double next = FloatingPreviewHoverAdjustment.AdjustOpacity(opacity, increase);

        Assert.Equal(expected, next, precision: 5);
    }

    [Fact]
    public void MatchesMouseWheelShortcut_RequiresExactModifiersAndMouseWheelKey()
    {
        var combination = new KeyCombination
        {
            Meta = true,
            Ctrl = true,
            Key = KeybindPrimaryKey.MouseWheel,
        };

        Assert.True(
            FloatingPreviewHoverAdjustment.MatchesMouseWheelShortcut(
                combination,
                ctrl: true,
                alt: false,
                shift: false,
                meta: true
            )
        );
        Assert.False(
            FloatingPreviewHoverAdjustment.MatchesMouseWheelShortcut(
                combination,
                ctrl: true,
                alt: false,
                shift: true,
                meta: true
            )
        );
        Assert.False(
            FloatingPreviewHoverAdjustment.MatchesMouseWheelShortcut(
                new KeyCombination { Meta = true, Ctrl = true, Key = KeybindPrimaryKey.A },
                ctrl: true,
                alt: false,
                shift: false,
                meta: true
            )
        );
    }
}
