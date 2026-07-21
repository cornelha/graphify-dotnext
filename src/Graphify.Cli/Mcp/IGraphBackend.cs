namespace Graphify.Cli.Mcp;

/// <summary>
/// Abstraction over graph data sources for MCP tool operations.
/// Supports both in-memory KnowledgeGraph (JSON) and SurrealDB backends.
/// Each method returns the same JSON response format for MCP client compatibility.
/// </summary>
public interface IGraphBackend
{
    Task<string> QueryAsync(string searchTerm, int limit, CancellationToken cancellationToken = default);
    Task<string> PathAsync(string sourceId, string targetId, CancellationToken cancellationToken = default);
    Task<string> ExplainAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<string> CommunitiesAsync(int? communityId, CancellationToken cancellationToken = default);
    Task<string> AnalyzeAsync(int topN, CancellationToken cancellationToken = default);
}
