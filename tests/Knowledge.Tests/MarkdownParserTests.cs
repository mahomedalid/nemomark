using Knowledge.Ingestion;

namespace Knowledge.Tests;

public class MarkdownParserTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void Parse_ExtractsTitleFromTopLevelHeading()
    {
        const string markdown = "# My Title\n\nSome intro.\n\n## Section A\n\nContent A.";

        var result = _parser.Parse(markdown);

        Assert.Equal("My Title", result.Title);
    }

    [Fact]
    public void Parse_SplitsSectionsByHeadings()
    {
        const string markdown = "# Title\n\nIntro text.\n\n## Section A\n\nContent A.\n\n## Section B\n\nContent B.";

        var result = _parser.Parse(markdown);

        Assert.Contains(result.Sections, s => s.Heading == "Section A");
        Assert.Contains(result.Sections, s => s.Heading == "Section B");
    }

    [Fact]
    public void Parse_UsesFallbackTitleWhenNoHeading()
    {
        const string markdown = "Just a paragraph with no heading.";

        var result = _parser.Parse(markdown, "fallback");

        Assert.Equal("fallback", result.Title);
    }
}
