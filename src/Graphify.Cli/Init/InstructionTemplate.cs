namespace Graphify.Cli.Init;

public static class InstructionTemplate
{
    /// <summary>
    /// Canonical MCP instructions written to .graphify/mcp-instructions.md.
    /// Kept intentionally concise (~400 tokens) for token efficiency.
    /// </summary>
    public const string CanonicalInstructions = """
# MCP Knowledge Graph: graphify

This project has a knowledge graph served via MCP (`graphify serve`).

## Tools
- **Analyze** -- Graph overview. Call this first (returns communities, god nodes, type distribution).
- **Communities** -- Pre-computed code modules (Louvain clusters, zero-token).
- **Query** -- Search nodes by name/label/type. Returns IDs for Explain/Path.
- **Explain** -- Full node details + all incoming/outgoing connections.
- **Path** -- Shortest path between two nodes (BFS, requires IDs).

## Investigation Protocol
1. `Analyze()` -> codebase shape (~200 tokens)
2. `Communities()` -> module clusters
3. `Query("term")` -> find specific nodes
4. `Explain("id")` -> deep-dive relationships
5. `Path("a", "b")` -> trace dependency chains

## When to Read Source
The graph captures structure and relationships only -- not method implementations.
After identifying relevant files via Explain/Query, read source files for implementation details.
""";

    public const string SnippetPointer = """
<!-- graphify:start -->
MCP knowledge graph available. See `.graphify/mcp-instructions.md` for tools and investigation protocol.
<!-- graphify:end -->
""";

    public const string SectionStartMarker = "<!-- graphify:start -->";
    public const string SectionEndMarker = "<!-- graphify:end -->";
}
