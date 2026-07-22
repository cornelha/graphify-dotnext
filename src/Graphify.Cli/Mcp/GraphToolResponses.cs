using System.Text.Json.Serialization;

namespace Graphify.Cli.Mcp;

/// <summary>
/// Shared DTOs used by both MemoryGraphBackend and SurrealDbGraphBackend
/// to ensure consistent JSON response shapes across backends.
/// </summary>

public sealed record QueryResult
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; init; }

    [JsonPropertyName("results")]
    public required List<NodeResult> Results { get; init; }
}

public sealed record NodeResult
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("confidence")]
    public string? Confidence { get; init; }

    [JsonPropertyName("community")]
    public int? Community { get; init; }

    [JsonPropertyName("degree")]
    public int Degree { get; init; }

    [JsonPropertyName("connections")]
    public List<ConnectionResult>? Connections { get; init; }
}

public sealed record ConnectionResult
{
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("target")]
    public required string Target { get; init; }

    [JsonPropertyName("relationship")]
    public required string Relationship { get; init; }

    [JsonPropertyName("weight")]
    public double Weight { get; init; }
}

public sealed record PathResult
{
    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("pathLength")]
    public int PathLength { get; init; }

    [JsonPropertyName("path")]
    public List<PathNodeResult>? Path { get; init; }
}

public sealed record PathNodeResult
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }
}

public sealed record ExplainResult
{
    [JsonPropertyName("node")]
    public required ExplainNodeResult Node { get; init; }

    [JsonPropertyName("statistics")]
    public required ExplainStatistics Statistics { get; init; }

    [JsonPropertyName("incomingEdges")]
    public List<EdgeResult>? IncomingEdges { get; init; }

    [JsonPropertyName("outgoingEdges")]
    public List<EdgeResult>? OutgoingEdges { get; init; }
}

public sealed record ExplainNodeResult
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("confidence")]
    public string? Confidence { get; init; }

    [JsonPropertyName("community")]
    public int? Community { get; init; }
}

public sealed record ExplainStatistics
{
    [JsonPropertyName("totalDegree")]
    public int TotalDegree { get; init; }

    [JsonPropertyName("incomingConnections")]
    public int IncomingConnections { get; init; }

    [JsonPropertyName("outgoingConnections")]
    public int OutgoingConnections { get; init; }
}

public sealed record EdgeResult
{
    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("fromLabel")]
    public string? FromLabel { get; init; }

    [JsonPropertyName("to")]
    public string? To { get; init; }

    [JsonPropertyName("toLabel")]
    public string? ToLabel { get; init; }

    [JsonPropertyName("relationship")]
    public required string Relationship { get; init; }

    [JsonPropertyName("confidence")]
    public string? Confidence { get; init; }
}

public sealed record CommunitiesListResult
{
    [JsonPropertyName("totalCommunities")]
    public int TotalCommunities { get; init; }

    [JsonPropertyName("nodesInCommunities")]
    public int NodesInCommunities { get; init; }

    [JsonPropertyName("nodesWithoutCommunity")]
    public int NodesWithoutCommunity { get; init; }

    [JsonPropertyName("communities")]
    public required List<CommunitySummaryResult> Communities { get; init; }
}

public sealed record CommunitySummaryResult
{
    [JsonPropertyName("communityId")]
    public int CommunityId { get; init; }

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; init; }

    [JsonPropertyName("topMembers")]
    public List<CommunityMemberResult>? TopMembers { get; init; }
}

public sealed record CommunityDetailResult
{
    [JsonPropertyName("communityId")]
    public int CommunityId { get; init; }

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; init; }

    [JsonPropertyName("members")]
    public required List<CommunityMemberResult> Members { get; init; }
}

public sealed record CommunityMemberResult
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    [JsonPropertyName("degree")]
    public int Degree { get; init; }
}

public sealed record AnalyzeResult
{
    [JsonPropertyName("statistics")]
    public required AnalyzeStatistics Statistics { get; init; }

    [JsonPropertyName("topNodes")]
    public required List<AnalyzeNodeResult> TopNodes { get; init; }

    [JsonPropertyName("nodeTypes")]
    public required List<TypeCountResult> NodeTypes { get; init; }

    [JsonPropertyName("relationshipTypes")]
    public required List<TypeCountResult> RelationshipTypes { get; init; }
}

public sealed record AnalyzeStatistics
{
    [JsonPropertyName("nodeCount")]
    public int NodeCount { get; init; }

    [JsonPropertyName("edgeCount")]
    public int EdgeCount { get; init; }

    [JsonPropertyName("communityCount")]
    public int CommunityCount { get; init; }

    [JsonPropertyName("averageDegree")]
    public double AverageDegree { get; init; }

    [JsonPropertyName("isolatedNodeCount")]
    public int IsolatedNodeCount { get; init; }
}

public sealed record AnalyzeNodeResult
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("degree")]
    public int Degree { get; init; }

    [JsonPropertyName("community")]
    public int? Community { get; init; }
}

public sealed record TypeCountResult
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; }
}

/// <summary>
/// Error response shape for all tools.
/// </summary>
public sealed record ErrorResult
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }
}
