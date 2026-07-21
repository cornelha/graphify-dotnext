using System.Text.Json;
using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Cli.Mcp;

/// <summary>
/// IGraphBackend implementation backed by an in-memory KnowledgeGraph (QuikGraph).
/// This is the same logic that was previously inlined in GraphTools.
/// </summary>
public sealed class MemoryGraphBackend : IGraphBackend
{
    private readonly KnowledgeGraph _graph;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MemoryGraphBackend(KnowledgeGraph graph)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    public Task<string> QueryAsync(string searchTerm, int limit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Task.FromResult(JsonSerializer.Serialize(new ErrorResult { Error = "Search term cannot be empty" }));
        }

        var searchLower = searchTerm.ToLowerInvariant();
        var matchingNodes = _graph.GetNodes()
            .Where(n => n.Id.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                       n.Label.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                       n.Type.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(n => new NodeResult
            {
                Id = n.Id,
                Label = n.Label,
                Type = n.Type,
                FilePath = n.FilePath,
                Language = n.Language,
                Confidence = n.Confidence.ToString(),
                Community = n.Community,
                Degree = _graph.GetDegree(n.Id),
                Connections = _graph.GetEdges(n.Id)
                    .Select(e => new ConnectionResult
                    {
                        Source = e.Source.Id,
                        Target = e.Target.Id,
                        Relationship = e.Relationship,
                        Weight = e.Weight
                    })
                    .Take(5)
                    .ToList()
            })
            .ToList();

        var result = new QueryResult
        {
            Query = searchTerm,
            ResultCount = matchingNodes.Count,
            Results = matchingNodes
        };

        return Task.FromResult(JsonSerializer.Serialize(result, JsonOptions));
    }

    public Task<string> PathAsync(string sourceId, string targetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
        {
            return Task.FromResult(JsonSerializer.Serialize(new ErrorResult { Error = "Source and target IDs are required" }));
        }

        var sourceNode = _graph.GetNode(sourceId);
        var targetNode = _graph.GetNode(targetId);

        if (sourceNode == null)
        {
            return Task.FromResult(JsonSerializer.Serialize(new ErrorResult { Error = $"Source node '{sourceId}' not found" }));
        }

        if (targetNode == null)
        {
            return Task.FromResult(JsonSerializer.Serialize(new ErrorResult { Error = $"Target node '{targetId}' not found" }));
        }

        try
        {
            var visited = new HashSet<string>();
            var queue = new Queue<(GraphNode Node, List<GraphNode> Path)>();
            queue.Enqueue((sourceNode, new List<GraphNode> { sourceNode }));
            visited.Add(sourceNode.Id);

            while (queue.Count > 0)
            {
                var (current, path) = queue.Dequeue();

                if (current.Id == targetNode.Id)
                {
                    var result = new PathResult
                    {
                        Found = true,
                        PathLength = path.Count - 1,
                        Path = path.Select(n => new PathNodeResult
                        {
                            Id = n.Id,
                            Label = n.Label,
                            Type = n.Type
                        }).ToList()
                    };

                    return Task.FromResult(JsonSerializer.Serialize(result, JsonOptions));
                }

                foreach (var neighbor in _graph.GetNeighbors(current.Id))
                {
                    if (!visited.Contains(neighbor.Id))
                    {
                        visited.Add(neighbor.Id);
                        var newPath = new List<GraphNode>(path) { neighbor };
                        queue.Enqueue((neighbor, newPath));
                    }
                }
            }

            return Task.FromResult(JsonSerializer.Serialize(new PathResult { Found = false }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(new ErrorResult { Error = $"Error finding path: {ex.Message}" }));
        }
    }

    public Task<string> ExplainAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Task.FromResult(JsonSerializer.Serialize(new ErrorResult { Error = "Node ID is required" }));
        }

        var node = _graph.GetNode(nodeId);
        if (node == null)
        {
            return Task.FromResult(JsonSerializer.Serialize(new ErrorResult { Error = $"Node '{nodeId}' not found" }));
        }

        var allEdges = _graph.GetEdges(nodeId).ToList();
        var inEdges = allEdges.Where(e => e.Target.Id == nodeId).ToList();
        var outEdges = allEdges.Where(e => e.Source.Id == nodeId).ToList();
        var degree = _graph.GetDegree(nodeId);

        var result = new ExplainResult
        {
            Node = new ExplainNodeResult
            {
                Id = node.Id,
                Label = node.Label,
                Type = node.Type,
                FilePath = node.FilePath,
                Language = node.Language,
                Confidence = node.Confidence.ToString(),
                Community = node.Community
            },
            Statistics = new ExplainStatistics
            {
                TotalDegree = degree,
                IncomingConnections = inEdges.Count,
                OutgoingConnections = outEdges.Count
            },
            IncomingEdges = inEdges.Select(e => new EdgeResult
            {
                From = e.Source.Id,
                FromLabel = e.Source.Label,
                Relationship = e.Relationship,
                Confidence = e.Confidence.ToString()
            }).ToList(),
            OutgoingEdges = outEdges.Select(e => new EdgeResult
            {
                To = e.Target.Id,
                ToLabel = e.Target.Label,
                Relationship = e.Relationship,
                Confidence = e.Confidence.ToString()
            }).ToList()
        };

        return Task.FromResult(JsonSerializer.Serialize(result, JsonOptions));
    }

    public Task<string> CommunitiesAsync(int? communityId, CancellationToken cancellationToken = default)
    {
        var allNodes = _graph.GetNodes().ToList();
        var nodesWithCommunities = allNodes.Where(n => n.Community.HasValue).ToList();

        if (communityId.HasValue)
        {
            var communityNodes = _graph.GetNodesByCommunity(communityId.Value).ToList();

            if (communityNodes.Count == 0)
            {
                return Task.FromResult(JsonSerializer.Serialize(new ErrorResult { Error = $"Community {communityId} not found or has no members" }));
            }

            var result = new CommunityDetailResult
            {
                CommunityId = communityId.Value,
                MemberCount = communityNodes.Count,
                Members = communityNodes.Select(n => new CommunityMemberResult
                {
                    Id = n.Id,
                    Label = n.Label,
                    Type = n.Type,
                    FilePath = n.FilePath,
                    Degree = _graph.GetDegree(n.Id)
                }).OrderByDescending(m => m.Degree).ToList()
            };

            return Task.FromResult(JsonSerializer.Serialize(result, JsonOptions));
        }

        var communities = nodesWithCommunities
            .GroupBy(n => n.Community!.Value)
            .Select(g => new CommunitySummaryResult
            {
                CommunityId = g.Key,
                MemberCount = g.Count(),
                TopMembers = g.OrderByDescending(n => _graph.GetDegree(n.Id))
                    .Take(5)
                    .Select(n => new CommunityMemberResult
                    {
                        Id = n.Id,
                        Label = n.Label,
                        Type = n.Type,
                        Degree = _graph.GetDegree(n.Id)
                    })
                    .ToList()
            })
            .OrderByDescending(c => c.MemberCount)
            .ToList();

        var listResult = new CommunitiesListResult
        {
            TotalCommunities = communities.Count,
            NodesInCommunities = nodesWithCommunities.Count,
            NodesWithoutCommunity = allNodes.Count - nodesWithCommunities.Count,
            Communities = communities
        };

        return Task.FromResult(JsonSerializer.Serialize(listResult, JsonOptions));
    }

    public Task<string> AnalyzeAsync(int topN, CancellationToken cancellationToken = default)
    {
        var allNodes = _graph.GetNodes().ToList();
        var allEdges = _graph.GetEdges().ToList();

        if (allNodes.Count == 0)
        {
            return Task.FromResult(JsonSerializer.Serialize(new ErrorResult { Error = "Graph is empty" }));
        }

        var topNodesByDegree = _graph.GetHighestDegreeNodes(topN).ToList();

        var nodesByCommunity = allNodes.Where(n => n.Community.HasValue)
            .GroupBy(n => n.Community!.Value)
            .Count();

        var isolatedNodes = allNodes.Where(n => _graph.GetDegree(n.Id) == 0).ToList();

        var nodesByType = allNodes.GroupBy(n => n.Type)
            .Select(g => new TypeCountResult { Type = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var edgesByRelationship = allEdges.GroupBy(e => e.Relationship)
            .Select(g => new TypeCountResult { Type = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var averageDegree = allNodes.Count > 0 ? allNodes.Average(n => _graph.GetDegree(n.Id)) : 0;

        var result = new AnalyzeResult
        {
            Statistics = new AnalyzeStatistics
            {
                NodeCount = allNodes.Count,
                EdgeCount = allEdges.Count,
                CommunityCount = nodesByCommunity,
                AverageDegree = Math.Round(averageDegree, 2),
                IsolatedNodeCount = isolatedNodes.Count
            },
            TopNodes = topNodesByDegree.Select(t => new AnalyzeNodeResult
            {
                Id = t.Node.Id,
                Label = t.Node.Label,
                Type = t.Node.Type,
                Degree = t.Degree,
                Community = t.Node.Community
            }).ToList(),
            NodeTypes = nodesByType,
            RelationshipTypes = edgesByRelationship
        };

        return Task.FromResult(JsonSerializer.Serialize(result, JsonOptions));
    }
}
