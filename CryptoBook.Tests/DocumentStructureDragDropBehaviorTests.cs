using CryptoBook.Behaviors;
using CryptoBook.DTO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DocumentStructureDragDropBehaviorTests
{
    [Theory]
    [InlineData(0, DocumentStructureDropPosition.Before)]
    [InlineData(9, DocumentStructureDropPosition.Before)]
    [InlineData(10, DocumentStructureDropPosition.Inside)]
    [InlineData(20, DocumentStructureDropPosition.Inside)]
    [InlineData(21, DocumentStructureDropPosition.After)]
    [InlineData(30, DocumentStructureDropPosition.After)]
    public void GetDropPosition_UsesThreeVerticalZones(
        double pointerY,
        DocumentStructureDropPosition expected)
    {
        Assert.Equal(
            expected,
            DocumentStructureDragDropBehavior.GetDropPosition(
                pointerY,
                itemHeight: 30));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(47, -1)]
    [InlineData(50, 0)]
    [InlineData(153, 1)]
    [InlineData(200, 1)]
    public void GetAutoScrollDirection_UsesEdgeZones(
        double pointerY,
        int expected)
    {
        Assert.Equal(
            expected,
            DocumentStructureDragDropBehavior.GetAutoScrollDirection(
                pointerY,
                viewportHeight: 200));
    }
}
