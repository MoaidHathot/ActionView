using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

public class BlockPathTests
{
    private static Entry SampleEntry() => new()
    {
        Type = "pr-review",
        Source = "test",
        Title = "PR",
        Content =
        [
            new ContentBlock { Type = ContentBlockType.Alert },                 // [0]
            new ContentBlock { Type = ContentBlockType.KeyValue },              // [1]
            new ContentBlock                                                    // [2] "Review Comments"
            {
                Type = ContentBlockType.Section,
                Title = "Review Comments",
                Children =
                [
                    new ContentBlock                                            // [2,0] a comment
                    {
                        Type = ContentBlockType.Section,
                        Id = "draft-abc",
                        Title = "Comment A",
                        Actions =
                        [
                            new EntryAction { Label = "Approve", Command = new ActionCommand { Type = CommandType.Cli, Program = "powerreview" } },
                            new EntryAction { Label = "Delete", Command = new ActionCommand { Type = CommandType.Cli, Program = "powerreview" } },
                        ],
                    },
                    new ContentBlock { Type = ContentBlockType.Section, Title = "Comment B" }, // [2,1]
                ],
            },
        ],
    };

    [Theory]
    [InlineData("3.0", new[] { 3, 0 })]
    [InlineData("0", new[] { 0 })]
    [InlineData(" 2 . 1 ", new[] { 2, 1 })]
    public void Parse_ValidPaths(string raw, int[] expected)
    {
        var parsed = BlockPath.Parse(raw);
        Assert.NotNull(parsed);
        Assert.Equal(expected, parsed!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a.b")]
    [InlineData("1.-1")]
    [InlineData("..")]
    public void Parse_InvalidPaths_ReturnNull(string raw)
    {
        Assert.Null(BlockPath.Parse(raw));
    }

    [Fact]
    public void Resolve_TopLevelSection()
    {
        var entry = SampleEntry();
        var block = BlockPath.Resolve(entry, [2]);
        Assert.NotNull(block);
        Assert.Equal("Review Comments", block!.Title);
    }

    [Fact]
    public void Resolve_NestedComment_ReachesActions()
    {
        // This is the exact case the old top-level-only scheme could not address.
        var entry = SampleEntry();
        var block = BlockPath.Resolve(entry, [2, 0]);
        Assert.NotNull(block);
        Assert.Equal("Comment A", block!.Title);
        Assert.Equal("draft-abc", block.Id);
        Assert.NotNull(block.Actions);
        Assert.Equal("Approve", block.Actions![0].Label);
    }

    [Fact]
    public void Resolve_OutOfRange_ReturnsNull()
    {
        var entry = SampleEntry();
        Assert.Null(BlockPath.Resolve(entry, [2, 5]));
        Assert.Null(BlockPath.Resolve(entry, [9]));
        Assert.Null(BlockPath.Resolve(entry, [0, 0])); // alert has no children
    }

    [Fact]
    public void Resolve_EmptyPath_ReturnsNull()
    {
        Assert.Null(BlockPath.Resolve(SampleEntry(), []));
    }
}
