using Graphify.Cli.Init;
using Xunit;

namespace Graphify.Tests.Init;

[Trait("Category", "Init")]
public sealed class InitServiceTests
{
    [Fact]
    public async Task RunAsync_WithNoAgents_DoesNotWriteInstructions()
    {
        using var dir = new TempDir();
        var output = new StringWriter();

        var service = new InitService(output, dir.Path);
        var exitCode = await service.RunAsync();

        Assert.Equal(0, exitCode);
        // No agents detected → no instructions written
        var canonicalPath = Path.Combine(dir.Path, ".graphify", "mcp-instructions.md");
        Assert.False(File.Exists(canonicalPath));
    }

    [Fact]
    public async Task RunAsync_WithInstallList_WritesInstructionsAndInjects()
    {
        using var dir = new TempDir();
        var output = new StringWriter();
        var agentFile = Path.Combine(dir.Path, "CLAUDE.md");

        var service = new InitService(output, dir.Path);
        var exitCode = await service.RunAsync(installAgents: "claude");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(agentFile));
        var content = File.ReadAllText(agentFile);
        Assert.Contains(InstructionTemplate.SectionStartMarker, content);
    }

    [Fact]
    public async Task RunAsync_WithMultipleInstallList_InjectsAll()
    {
        using var dir = new TempDir();
        var output = new StringWriter();
        var claudeFile = Path.Combine(dir.Path, "CLAUDE.md");
        var cursorFile = Path.Combine(dir.Path, ".cursorrules");

        var service = new InitService(output, dir.Path);
        var exitCode = await service.RunAsync(installAgents: "claude,cursor");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(claudeFile));
        Assert.True(File.Exists(cursorFile));
        Assert.Contains(InstructionTemplate.SectionStartMarker, File.ReadAllText(claudeFile));
        Assert.Contains(InstructionTemplate.SectionStartMarker, File.ReadAllText(cursorFile));
    }

    [Fact]
    public async Task RunAsync_WithForce_RegeneratesExisting()
    {
        using var dir = new TempDir();
        var output = new StringWriter();

        var service = new InitService(output, dir.Path);
        await service.RunAsync(installAgents: "claude");

        // Run again with force
        var exitCode = await service.RunAsync(installAgents: "claude", force: true);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_Uninstall_RemovesMarkers()
    {
        using var dir = new TempDir();
        var output = new StringWriter();
        var agentFile = Path.Combine(dir.Path, "CLAUDE.md");

        // Install first
        var service = new InitService(output, dir.Path);
        await service.RunAsync(installAgents: "claude");

        Assert.Contains(InstructionTemplate.SectionStartMarker, File.ReadAllText(agentFile));

        // Uninstall
        output = new StringWriter();
        service = new InitService(output, dir.Path);
        var exitCode = await service.RunAsync(uninstall: true);

        Assert.Equal(0, exitCode);
        if (File.Exists(agentFile))
        {
            Assert.DoesNotContain(InstructionTemplate.SectionStartMarker, File.ReadAllText(agentFile));
        }
    }

    [Fact]
    public async Task RunAsync_InvalidAgentName_Ignores()
    {
        using var dir = new TempDir();
        var output = new StringWriter();

        var service = new InitService(output, dir.Path);
        var exitCode = await service.RunAsync(installAgents: "nonexistent_agent");

        Assert.Equal(0, exitCode);
    }
}
