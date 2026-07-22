namespace Graphify.Cli.Init;

public static class AgentDetector
{
    public static List<AgentType> Detect(string projectDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDir);

        var detected = new List<AgentType>();

        // Claude Code: ./CLAUDE.md
        if (File.Exists(Path.Combine(projectDir, "CLAUDE.md")))
            detected.Add(AgentType.Claude);

        // Copilot: ./.github/copilot-instructions.md
        if (File.Exists(Path.Combine(projectDir, ".github", "copilot-instructions.md")))
            detected.Add(AgentType.Copilot);

        // OpenCode: ./.opencode/instructions.md
        if (File.Exists(Path.Combine(projectDir, ".opencode", "instructions.md")))
            detected.Add(AgentType.OpenCode);

        // Qoder: ./.squad/config.json
        if (File.Exists(Path.Combine(projectDir, ".squad", "config.json")))
            detected.Add(AgentType.Qoder);

        // Cursor: ./.cursorrules
        if (File.Exists(Path.Combine(projectDir, ".cursorrules")))
            detected.Add(AgentType.Cursor);

        // Windsurf: ./.windsurfrules
        if (File.Exists(Path.Combine(projectDir, ".windsurfrules")))
            detected.Add(AgentType.Windsurf);

        return detected;
    }
}
