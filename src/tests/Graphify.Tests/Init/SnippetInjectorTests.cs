using Graphify.Cli.Init;
using Xunit;

namespace Graphify.Tests.Init;

[Trait("Category", "Init")]
public sealed class SnippetInjectorTests
{
    [Fact]
    public void WriteCanonicalInstructions_CreatesFile()
    {
        using var dir = new TempDir();

        var written = SnippetInjector.WriteCanonicalInstructions(dir.Path, force: false);

        Assert.True(written);
        var filePath = Path.Combine(dir.Path, ".graphify", "mcp-instructions.md");
        Assert.True(File.Exists(filePath));
        var content = File.ReadAllText(filePath);
        Assert.Contains("MCP Knowledge Graph: graphify", content);
        Assert.Contains("## Tools", content);
        Assert.Contains("## Investigation Protocol", content);
    }

    [Fact]
    public void WriteCanonicalInstructions_IsIdempotent()
    {
        using var dir = new TempDir();

        var first = SnippetInjector.WriteCanonicalInstructions(dir.Path, force: false);
        var second = SnippetInjector.WriteCanonicalInstructions(dir.Path, force: false);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void WriteCanonicalInstructions_WithForce_Overwrites()
    {
        using var dir = new TempDir();

        SnippetInjector.WriteCanonicalInstructions(dir.Path, force: false);
        var overwritten = SnippetInjector.WriteCanonicalInstructions(dir.Path, force: true);

        Assert.True(overwritten);
    }

    [Fact]
    public void InjectSnippet_AddsPointerToFile()
    {
        using var dir = new TempDir();
        var agentFile = Path.Combine(dir.Path, "CLAUDE.md");
        File.WriteAllText(agentFile, "# Claude instructions");

        var result = SnippetInjector.InjectSnippet(agentFile);

        Assert.True(result);
        var content = File.ReadAllText(agentFile);
        Assert.Contains(InstructionTemplate.SectionStartMarker, content);
        Assert.Contains(InstructionTemplate.SectionEndMarker, content);
        Assert.Contains("MCP knowledge graph available", content);
    }

    [Fact]
    public void InjectSnippet_IsIdempotent()
    {
        using var dir = new TempDir();
        var agentFile = Path.Combine(dir.Path, "CLAUDE.md");
        File.WriteAllText(agentFile, "# Claude instructions");

        var first = SnippetInjector.InjectSnippet(agentFile);
        var second = SnippetInjector.InjectSnippet(agentFile);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void InjectSnippet_CreatesFileIfMissing()
    {
        using var dir = new TempDir();
        var agentFile = Path.Combine(dir.Path, "CLAUDE.md");

        var result = SnippetInjector.InjectSnippet(agentFile);

        Assert.True(result);
        Assert.True(File.Exists(agentFile));
    }

    [Fact]
    public void RemoveSnippet_RemovesMarkers()
    {
        using var dir = new TempDir();
        var agentFile = Path.Combine(dir.Path, "CLAUDE.md");
        File.WriteAllText(agentFile, "# Original content\n");
        SnippetInjector.InjectSnippet(agentFile);

        var removed = SnippetInjector.RemoveSnippet(agentFile);

        Assert.True(removed);
        var content = File.ReadAllText(agentFile);
        Assert.DoesNotContain(InstructionTemplate.SectionStartMarker, content);
        Assert.DoesNotContain(InstructionTemplate.SectionEndMarker, content);
        Assert.Contains("# Original content", content);
    }

    [Fact]
    public void RemoveSnippet_WhenNoSnippet_ReturnsFalse()
    {
        using var dir = new TempDir();
        var agentFile = Path.Combine(dir.Path, "CLAUDE.md");
        File.WriteAllText(agentFile, "# No snippet here");

        var removed = SnippetInjector.RemoveSnippet(agentFile);

        Assert.False(removed);
    }

    [Fact]
    public void RemoveSnippet_WhenFileNotExists_ReturnsFalse()
    {
        var removed = SnippetInjector.RemoveSnippet(@"X:\nonexistent\file.md");

        Assert.False(removed);
    }

    [Fact]
    public void RemoveCanonicalInstructions_DeletesFileAndEmptyDir()
    {
        using var dir = new TempDir();
        SnippetInjector.WriteCanonicalInstructions(dir.Path, force: false);

        var removed = SnippetInjector.RemoveCanonicalInstructions(dir.Path);

        Assert.True(removed);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, ".graphify")));
    }

    [Fact]
    public void RemoveCanonicalInstructions_WhenNotExists_ReturnsFalse()
    {
        using var dir = new TempDir();

        var removed = SnippetInjector.RemoveCanonicalInstructions(dir.Path);

        Assert.False(removed);
    }
}
