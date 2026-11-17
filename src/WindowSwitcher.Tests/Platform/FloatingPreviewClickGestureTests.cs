using WindowSwitcher.Lib.Data.Platform;
using Xunit;

namespace WindowSwitcher.Tests.Platform;

public sealed class FloatingPreviewClickGestureTests
{
    [Theory]
    [InlineData(0, 10, 20, 10, 20, false, false, true)]
    [InlineData(399, 10, 20, 14, 20, false, false, true)]
    [InlineData(399, 10, 20, 10, 24, false, false, true)]
    [InlineData(400, 10, 20, 10, 20, false, false, false)]
    [InlineData(800, 10, 20, 10, 20, false, false, false)]
    [InlineData(100, 10, 20, 15, 20, false, false, false)]
    [InlineData(100, 10, 20, 10, 25, false, false, false)]
    [InlineData(100, 0, 0, -5, 0, false, false, false)]
    [InlineData(100, 10, 20, 10, 20, true, false, false)]
    [InlineData(100, 10, 20, 10, 20, false, true, false)]
    [InlineData(-1, 10, 20, 10, 20, false, false, false)]
    public void IsActivationClick_RequiresShortStationaryLeftPress(
        long holdDurationMilliseconds,
        int startX,
        int startY,
        int endX,
        int endY,
        bool resizeDragStarted,
        bool movedBeyondThreshold,
        bool expected
    )
    {
        bool activate = FloatingPreviewClickGesture.IsActivationClick(
            holdDurationMilliseconds,
            startX,
            startY,
            endX,
            endY,
            resizeDragStarted,
            movedBeyondThreshold
        );

        Assert.Equal(expected, activate);
    }

    [Fact]
    public void ExceedsDragThreshold_IgnoresMovementWithinFourPixels()
    {
        Assert.False(FloatingPreviewClickGesture.ExceedsDragThreshold(0, 0, 4, -4));
        Assert.True(FloatingPreviewClickGesture.ExceedsDragThreshold(0, 0, 5, 0));
    }
}
