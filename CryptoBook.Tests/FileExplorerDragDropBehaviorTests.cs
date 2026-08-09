using CryptoBook.Behaviors;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileExplorerDragDropBehaviorTests
{
    [Theory]
    [InlineData(0, 300, -1)]
    [InlineData(35, 300, -1)]
    [InlineData(150, 300, 0)]
    [InlineData(265, 300, 1)]
    [InlineData(300, 300, 1)]
    public void GetAutoScrollDirection_UsesTopAndBottomEdgeZones(
        double pointerY,
        double viewportHeight,
        int expectedDirection)
    {
        Assert.Equal(
            expectedDirection,
            FileExplorerDragDropBehavior.GetAutoScrollDirection(
                pointerY,
                viewportHeight));
    }
}
