using System.ComponentModel;
using Graphify.Graph;
using Graphify.Models;
using ModelContextProtocol.Server;

namespace Graphify.Cli.Mcp;

[McpServerToolType]
public class GraphTools
{
    private readonly IGraphBackend _backend;

    public GraphTools(IGraphBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    [McpServerTool]
    [Description("Search nodes and edges by name, label, or type.")]
    public async Task<string> Query(
        [Description("Match against node ID, label, or type")]
        string searchTerm,
        [Description("Max results (default 10)")]
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return await _backend.QueryAsync(searchTerm, limit, cancellationToken);
    }

    [McpServerTool]
    [Description("Shortest path between two nodes.")]
    public async Task<string> Path(
        [Description("Start node ID")]
        string sourceId,
        [Description("End node ID")]
        string targetId,
        CancellationToken cancellationToken = default)
    {
        return await _backend.PathAsync(sourceId, targetId, cancellationToken);
    }

    [McpServerTool]
    [Description("Node details with all connections.")]
    public async Task<string> Explain(
        [Description("Node ID")]
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return await _backend.ExplainAsync(nodeId, cancellationToken);
    }

    [McpServerTool]
    [Description("List communities and their members.")]
    public async Task<string> Communities(
        [Description("Specific community (omit for all)")]
        int? communityId = null,
        CancellationToken cancellationToken = default)
    {
        return await _backend.CommunitiesAsync(communityId, cancellationToken);
    }

    [McpServerTool]
    [Description("Graph-wide statistics and structure.")]
    public async Task<string> Analyze(
        [Description("Top nodes to include (default 10)")]
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        return await _backend.AnalyzeAsync(topN, cancellationToken);
    }
}
