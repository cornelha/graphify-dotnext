using Graphify.Cli.Init;
using Xunit;

namespace Graphify.Tests.Init;

[Trait("Category", "Init")]
public sealed class AgentDetectorTests
{
    [Fact]
    public void Detect_WhenClaudeMarkdownExists_ReturnsClaude()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "CLAUDE.md"), "# Claude instructions");

        var agents = AgentDetector.Detect(dir.Path);

        Assert.Contains(AgentType.Claude, agents);
    }

    [Fact]
    public void Detect_WhenCopilotInstructionsExist_ReturnsCopilot()
    {
        using var dir = new TempDir();
        var copilotDir = Path.Combine(dir.Path, ".github");
        Directory.CreateDirectory(copilotDir);
        File.WriteAllText(Path.Combine(copilotDir, "copilot-instructions.md"), "# Copilot instructions");

        var agents = AgentDetector.Detect(dir.Path);

        Assert.Contains(AgentType.Copilot, agents);
    }

    [Fact]
    public void Detect_WhenOpenCodeInstructionsExist_ReturnsOpenCode()
    {
        using var dir = new TempDir();
        var opencodeDir = Path.Combine(dir.Path, ".opencode");
        Directory.CreateDirectory(opencodeDir);
        File.WriteAllText(Path.Combine(opencodeDir, "instructions.md"), "# OpenCode instructions");

        var agents = AgentDetector.Detect(dir.Path);

        Assert.Contains(AgentType.OpenCode, agents);
    }

    [Fact]
    public void Detect_WhenQoderConfigExists_ReturnsQoder()
    {
        using var dir = new TempDir();
        var squadDir = Path.Combine(dir.Path, ".squad");
        Directory.CreateDirectory(squadDir);
        File.WriteAllText(Path.Combine(squadDir, "config.json"), "{}");

        var agents = AgentDetector.Detect(dir.Path);

        Assert.Contains(AgentType.Qoder, agents);
    }

    [Fact]
    public void Detect_WhenCursorRulesExists_ReturnsCursor()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, ".cursorrules"), "# Cursor rules");

        var agents = AgentDetector.Detect(dir.Path);

        Assert.Contains(AgentType.Cursor, agents);
    }

    [Fact]
    public void Detect_WhenWindsurfRulesExists_ReturnsWindsurf()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, ".windsurfrules"), "# Windsurf rules");

        var agents = AgentDetector.Detect(dir.Path);

        Assert.Contains(AgentType.Windsurf, agents);
    }

    [Fact]
    public void Detect_WhenMultipleAgentsExist_ReturnsAll()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "CLAUDE.md"), "# Claude");
        File.WriteAllText(Path.Combine(dir.Path, ".cursorrules"), "# Cursor");

        var agents = AgentDetector.Detect(dir.Path);

        Assert.Contains(AgentType.Claude, agents);
        Assert.Contains(AgentType.Cursor, agents);
        Assert.Equal(2, agents.Count);
    }

    [Fact]
    public void Detect_WhenNoAgentFiles_ReturnsEmpty()
    {
        using var dir = new TempDir();

        var agents = AgentDetector.Detect(dir.Path);

        Assert.Empty(agents);
    }

    [Fact]
    public void Detect_WhenDirectoryNotExist_ReturnsEmpty()
    {
        var agents = AgentDetector.Detect(@"X:\nonexistent\path_" + Guid.NewGuid());

        Assert.Empty(agents);
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gfx-test-" + Guid.NewGuid().ToString("N")[..12]);

    public TempDir()
    {
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}
