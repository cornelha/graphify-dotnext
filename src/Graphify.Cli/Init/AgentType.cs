namespace Graphify.Cli.Init;

[Flags]
public enum AgentType
{
    Claude = 1,
    Copilot = 2,
    OpenCode = 4,
    Qoder = 8,
    Cursor = 16,
    Windsurf = 32
}
