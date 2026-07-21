namespace Graphify.Cli.Init;

public static class AgentFileResolver
{
    public static string GetInstructionFilePath(AgentType agent, string projectDir)
    {
        return agent switch
        {
            AgentType.Claude => Path.Combine(projectDir, "CLAUDE.md"),
            AgentType.Copilot => Path.Combine(projectDir, ".github", "copilot-instructions.md"),
            AgentType.OpenCode => Path.Combine(projectDir, ".opencode", "instructions.md"),
            AgentType.Qoder => Path.Combine(projectDir, ".squad", "instructions.md"),
            AgentType.Cursor => Path.Combine(projectDir, ".cursorrules"),
            AgentType.Windsurf => Path.Combine(projectDir, ".windsurfrules"),
            _ => throw new ArgumentOutOfRangeException(nameof(agent), $"Unknown agent type: {agent}")
        };
    }
}
