using VsDebugBridge.Contracts;

namespace VsDebugBridge.Tests;

public sealed class DebugLocationSelectorTests
{
    [Fact]
    public void BreakModePrefersStackFrameLocationOverActiveDocument()
    {
        var activeDocumentLocation = new DocumentLocation
        {
            FilePath = @"C:\sample\Program.cs",
            Line = 20,
            Column = 6
        };
        var stackFrameLocation = new DocumentLocation
        {
            FilePath = @"C:\sample\Program.cs",
            Line = 29
        };

        var selected = DebugLocationSelector.SelectCurrentLocation(
            DebuggerState.Break,
            activeDocumentLocation,
            stackFrameLocation);

        Assert.NotNull(selected);
        Assert.Equal(29, selected.Line);
        Assert.Null(selected.Column);
    }

    [Fact]
    public void BreakModeKeepsActiveDocumentColumnWhenItMatchesStackFrameLine()
    {
        var activeDocumentLocation = new DocumentLocation
        {
            FilePath = @"C:\sample\Program.cs",
            Line = 29,
            Column = 5
        };
        var stackFrameLocation = new DocumentLocation
        {
            FilePath = @"C:\SAMPLE\Program.cs",
            Line = 29
        };

        var selected = DebugLocationSelector.SelectCurrentLocation(
            DebuggerState.Break,
            activeDocumentLocation,
            stackFrameLocation);

        Assert.NotNull(selected);
        Assert.Equal(29, selected.Line);
        Assert.Equal(5, selected.Column);
    }

    [Fact]
    public void RunModeUsesActiveDocumentLocation()
    {
        var activeDocumentLocation = new DocumentLocation
        {
            FilePath = @"C:\sample\Program.cs",
            Line = 20,
            Column = 6
        };
        var stackFrameLocation = new DocumentLocation
        {
            FilePath = @"C:\sample\Program.cs",
            Line = 29
        };

        var selected = DebugLocationSelector.SelectCurrentLocation(
            DebuggerState.Run,
            activeDocumentLocation,
            stackFrameLocation);

        Assert.NotNull(selected);
        Assert.Equal(20, selected.Line);
        Assert.Equal(6, selected.Column);
    }
}
