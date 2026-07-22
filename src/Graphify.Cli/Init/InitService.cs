namespace Graphify.Cli.Init;

public sealed class InitService
{
    private readonly TextWriter _output;
    private readonly string _projectDir;

    public InitService(TextWriter output, string projectDir)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _projectDir = projectDir ?? throw new ArgumentNullException(nameof(projectDir));
    }

    public async Task<int> RunAsync(
        string? installAgents = null,
        bool uninstall = false,
        bool force = false)
    {
        if (uninstall)
        {
            return await UninstallAsync();
        }

        // Determine which agents to target
        List<AgentType> targets;
        if (!string.IsNullOrWhiteSpace(installAgents))
        {
            targets = ParseAgentList(installAgents);
        }
        else
        {
            targets = AgentDetector.Detect(_projectDir);
        }

        if (targets.Count == 0)
        {
            await _output.WriteLineAsync("No supported coding agents detected.");
            await _output.WriteLineAsync("To target specific agents, use:");
            await _output.WriteLineAsync("  graphify init --install copilot,claude");
            await _output.WriteLineAsync();
            await _output.WriteLineAsync("Supported agents: claude, copilot, opencode, qoder, cursor, windsurf");
            return 0;
        }

        // 1. Write canonical instructions
        var written = SnippetInjector.WriteCanonicalInstructions(_projectDir, force);
        if (written)
        {
            await _output.WriteLineAsync("Wrote canonical instructions to .graphify/mcp-instructions.md");
        }
        else
        {
            await _output.WriteLineAsync("Canonical instructions already present (.graphify/mcp-instructions.md). Use --force to regenerate.");
        }

        // 2. Inject pointer snippets for each agent
        var injected = 0;
        var skipped = 0;

        foreach (var agent in targets)
        {
            var filePath = AgentFileResolver.GetInstructionFilePath(agent, _projectDir);
            var agentName = agent.ToString().ToLowerInvariant();

            if (SnippetInjector.InjectSnippet(filePath))
            {
                await _output.WriteLineAsync($"  Injected pointer into {agentName} ({filePath})");
                injected++;
            }
            else
            {
                skipped++;
            }
        }

        if (injected > 0)
        {
            await _output.WriteLineAsync($"Configured {injected} agent(s) with MCP knowledge graph pointers.");
        }
        if (skipped > 0)
        {
            await _output.WriteLineAsync($"{skipped} agent(s) already configured (use --force to overwrite).");
        }

        return 0;
    }

    private async Task<int> UninstallAsync()
    {
        var targets = AgentDetector.Detect(_projectDir);
        var removed = 0;

        foreach (var agent in targets)
        {
            var filePath = AgentFileResolver.GetInstructionFilePath(agent, _projectDir);
            if (SnippetInjector.RemoveSnippet(filePath))
            {
                await _output.WriteLineAsync($"  Removed pointer from {agent.ToString().ToLowerInvariant()} ({filePath})");
                removed++;
            }
        }

        if (SnippetInjector.RemoveCanonicalInstructions(_projectDir))
        {
            await _output.WriteLineAsync("Removed .graphify/mcp-instructions.md");
        }

        if (removed == 0 && !File.Exists(Path.Combine(_projectDir, ".graphify", "mcp-instructions.md")))
        {
            await _output.WriteLineAsync("No graphify MCP instructions found to remove.");
        }
        else
        {
            await _output.WriteLineAsync($"Removed graphify MCP instructions from {removed} agent(s).");
        }

        return 0;
    }

    private static List<AgentType> ParseAgentList(string agents)
    {
        var result = new List<AgentType>();
        var parts = agents.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var agent = part.ToLowerInvariant() switch
            {
                "claude" => AgentType.Claude,
                "copilot" => AgentType.Copilot,
                "opencode" => AgentType.OpenCode,
                "qoder" => AgentType.Qoder,
                "cursor" => AgentType.Cursor,
                "windsurf" => AgentType.Windsurf,
                _ => (AgentType?)null
            };

            if (agent.HasValue && !result.Contains(agent.Value))
                result.Add(agent.Value);
        }

        return result;
    }
}
