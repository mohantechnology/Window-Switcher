using WindowSwitcher.Lib.Models;
using Xunit;

namespace WindowSwitcher.Tests.Models;

public sealed class FloatingWindowLabelTextTests
{
    [Fact]
    public void Format_ReturnsEmptyText_WhenTitleAndProcessAreBlank()
    {
        Assert.Equal(string.Empty, FloatingWindowLabelText.Format(null, null));
        Assert.Equal(string.Empty, FloatingWindowLabelText.Format("   ", " "));
    }

    [Fact]
    public void Format_ReturnsShortTitle_WhenItAlreadyNamesTheProcess()
    {
        Assert.Equal("Notepad", FloatingWindowLabelText.Format("Notepad", "notepad"));
    }

    [Fact]
    public void Format_AppendsProcessName_WhenTheTitleHasNoApplicationSuffix()
    {
        Assert.Equal(
            "Meeting notes - notepad",
            FloatingWindowLabelText.Format("Meeting notes", "notepad")
        );
    }

    [Fact]
    public void Format_KeepsCaptionApplication_WhenTheDocumentIsLong()
    {
        string title =
            "Quarterly Report Final Version Reviewed by Finance - Microsoft Excel";

        Assert.Equal(
            "Quarterly Repor... - Microsoft Excel",
            FloatingWindowLabelText.Format(title, "EXCEL")
        );
    }

    [Fact]
    public void Format_UsesTheLastCaptionSeparator()
    {
        Assert.Equal(
            "Draft - Budget... - Microsoft Excel",
            FloatingWindowLabelText.Format("Draft - Budget workbook - Microsoft Excel", "EXCEL")
        );
    }

    [Fact]
    public void Format_DoesNotAddEllipsis_WhenTheDocumentIsExactlyFifteenCharacters()
    {
        Assert.Equal(
            "123456789012345 - Notepad",
            FloatingWindowLabelText.Format("123456789012345 - Notepad", "notepad")
        );
    }

    [Fact]
    public void Format_TruncatesTheSixteenthCharacter()
    {
        Assert.Equal(
            "123456789012345... - notepad",
            FloatingWindowLabelText.Format("1234567890123456", "notepad")
        );
    }

    [Fact]
    public void Format_FallsBackToProcessName_WhenTheCaptionSuffixIsTooLongToBeAnAppName()
    {
        string suffix = new string('x', 41);
        Assert.Equal(
            "Notes - notepad",
            FloatingWindowLabelText.Format($"Notes - {suffix}", "notepad")
        );
    }

    [Fact]
    public void Format_ReturnsProcessName_WhenTheTitleIsEmpty()
    {
        Assert.Equal("chrome", FloatingWindowLabelText.Format(null, " chrome "));
    }
}
