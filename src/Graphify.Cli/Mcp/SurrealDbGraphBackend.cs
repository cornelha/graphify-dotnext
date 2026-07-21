using System.Text.Json;
using SurrealDb.Net;
using SurrealDb.Net.Models;
using SurrealDb.Net.Models.Response;

namespace Graphify.Cli.Mcp;

/// <summary>
/// IGraphBackend implementation that queries a SurrealDB database directly via SurrealQL.
/// Used for the MCP serve command when --surreal-endpoint or --surreal-path is specified.
/// </summary>
public sealed class SurrealDbGraphBackend : IGraphBackend, IAsyncDisposable
{
    private readonly ISurrealDbClient _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Cache for Path BFS — loaded lazily
    private Dictionary<string, SurrealEntityRecord>? _entitiesCache;
    private List<SurrealRelationshipRecord>? _relationshipsCache;
    private readonly object _cacheLock = new();

    public SurrealDbGraphBackend(ISurrealDbClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }

    public async Task<string> QueryAsync(string searchTerm, int limit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return JsonSerializer.Serialize(new ErrorResult { Error = "Search term cannot be empty" });

        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["term"] = searchTerm.ToLowerInvariant(),
                ["limit"] = limit
            };

            var response = await _db.RawQuery(
                "SELECT * FROM entity WHERE string::contains(string::lowercase(id), $term) OR string::contains(string::lowercase(label), $term) OR string::contains(string::lowercase(kind), $term) LIMIT $limit",
                parameters,
                cancellationToken
            );

            if (response.FirstOk is not { } ok)
                return JsonSerializer.Serialize(new ErrorResult { Error = "Query failed" });

            var entities = ok.GetValues<SurrealEntityRecord>().ToList();
            var results = new List<NodeResult>();

            foreach (var entity in entities)
            {
                var nodeId = ExtractNodeId(entity.Id);
                var connections = await FetchConnectionsAsync(entity.Id, cancellationToken);
                var degree = connections.Count;

                results.Add(new NodeResult
                {
                    Id = nodeId,
                    Label = entity.label ?? nodeId,
                    Type = entity.kind ?? "unknown",
                    FilePath = entity.filePath,
                    Language = entity.language,
                    Confidence = entity.confidence,
                    Community = entity.community,
                    Degree = degree,
                    Connections = connections.Take(5).ToList()
                });
            }

            return JsonSerializer.Serialize(new QueryResult
            {
                Query = searchTerm,
                ResultCount = results.Count,
                Results = results
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Query failed: {ex.Message}" });
        }
    }

    public async Task<string> ExplainAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return JsonSerializer.Serialize(new ErrorResult { Error = "Node ID is required" });

        try
        {
            var recordId = MakeEntityRecordId(nodeId);

            // Fetch node
            var nodeResponse = await _db.RawQuery(
                "SELECT * FROM $node_id",
                new Dictionary<string, object?> { ["node_id"] = recordId },
                cancellationToken
            );

            if (nodeResponse.FirstOk is not { } nodeOk)
                return JsonSerializer.Serialize(new ErrorResult { Error = $"Node '{nodeId}' not found" });

            var entity = nodeOk.GetValues<SurrealEntityRecord>().FirstOrDefault();
            if (entity == null)
                return JsonSerializer.Serialize(new ErrorResult { Error = $"Node '{nodeId}' not found" });

            // Fetch incoming and outgoing relationships
            var relResponse = await _db.RawQuery(
                "SELECT * FROM relationship WHERE source = $record_id OR target = $record_id",
                new Dictionary<string, object?> { ["record_id"] = recordId },
                cancellationToken
            );

            var inEdges = new List<EdgeResult>();
            var outEdges = new List<EdgeResult>();

            if (relResponse.FirstOk is { } relOk)
            {
                var relationships = relOk.GetValues<SurrealRelationshipRecord>().ToList();

                foreach (var rel in relationships)
                {
                    var sourceId = ExtractNodeId(rel.source);
                    var targetId = ExtractNodeId(rel.target);

                    if (targetId == nodeId)
                    {
                        inEdges.Add(new EdgeResult
                        {
                            From = sourceId,
                            FromLabel = sourceId,
                            Relationship = rel.type ?? "",
                            Confidence = rel.confidence
                        });
                    }

                    if (sourceId == nodeId)
                    {
                        outEdges.Add(new EdgeResult
                        {
                            To = targetId,
                            ToLabel = targetId,
                            Relationship = rel.type ?? "",
                            Confidence = rel.confidence
                        });
                    }
                }
            }

            var degree = inEdges.Count + outEdges.Count;

            return JsonSerializer.Serialize(new ExplainResult
            {
                Node = new ExplainNodeResult
                {
                    Id = ExtractNodeId(entity.Id),
                    Label = entity.label ?? nodeId,
                    Type = entity.kind ?? "unknown",
                    FilePath = entity.filePath,
                    Language = entity.language,
                    Confidence = entity.confidence,
                    Community = entity.community
                },
                Statistics = new ExplainStatistics
                {
                    TotalDegree = degree,
                    IncomingConnections = inEdges.Count,
                    OutgoingConnections = outEdges.Count
                },
                IncomingEdges = inEdges,
                OutgoingEdges = outEdges
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Explain failed: {ex.Message}" });
        }
    }

    public async Task<string> PathAsync(string sourceId, string targetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
            return JsonSerializer.Serialize(new ErrorResult { Error = "Source and target IDs are required" });

        try
        {
            await EnsurePathCacheLoadedAsync(cancellationToken);

            if (_entitiesCache == null || !_entitiesCache.ContainsKey(sourceId))
                return JsonSerializer.Serialize(new ErrorResult { Error = $"Source node '{sourceId}' not found" });

            if (!_entitiesCache.ContainsKey(targetId))
                return JsonSerializer.Serialize(new ErrorResult { Error = $"Target node '{targetId}' not found" });

            // Build adjacency list
            var adjacency = new Dictionary<string, List<string>>();
            foreach (var node in _entitiesCache.Keys)
                adjacency[node] = [];

            if (_relationshipsCache != null)
            {
                foreach (var rel in _relationshipsCache)
                {
                    var src = ExtractNodeId(rel.source);
                    var tgt = ExtractNodeId(rel.target);
                    if (adjacency.ContainsKey(src))
                        adjacency[src].Add(tgt);
                }
            }

            // BFS
            var visited = new HashSet<string> { sourceId };
            var queue = new Queue<(string Node, List<string> Path)>();
            queue.Enqueue((sourceId, [sourceId]));

            while (queue.Count > 0)
            {
                var (current, path) = queue.Dequeue();

                if (current == targetId)
                {
                    return JsonSerializer.Serialize(new PathResult
                    {
                        Found = true,
                        PathLength = path.Count - 1,
                        Path = path.Select(id => _entitiesCache!.TryGetValue(id, out var e)
                            ? new PathNodeResult
                            {
                                Id = id,
                                Label = e.label ?? id,
                                Type = e.kind ?? "unknown"
                            }
                            : new PathNodeResult { Id = id, Label = id, Type = "unknown" }).ToList()
                    }, JsonOptions);
                }

                if (!adjacency.TryGetValue(current, out var neighbors))
                    continue;

                foreach (var neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                    {
                        var newPath = new List<string>(path) { neighbor };
                        queue.Enqueue((neighbor, newPath));
                    }
                }
            }

            return JsonSerializer.Serialize(new PathResult { Found = false }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Path failed: {ex.Message}" });
        }
    }

    public async Task<string> CommunitiesAsync(int? communityId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (communityId.HasValue)
            {
                var parameters = new Dictionary<string, object?>
                {
                    ["communityId"] = communityId.Value
                };

                var response = await _db.RawQuery(
                    "SELECT * FROM entity WHERE community = $communityId ORDER BY community DESC",
                    parameters,
                    cancellationToken
                );

                if (response.FirstOk is not { } ok)
                    return JsonSerializer.Serialize(new ErrorResult { Error = $"Community {communityId} not found" });

                var members = ok.GetValues<SurrealEntityRecord>().ToList();

                if (members.Count == 0)
                    return JsonSerializer.Serialize(new ErrorResult { Error = $"Community {communityId} not found or has no members" });

                // Fetch degree info by counting relationships for each member
                var memberResults = new List<CommunityMemberResult>();
                foreach (var member in members)
                {
                    var id = ExtractNodeId(member.Id);
                    var connections = await FetchConnectionsAsync(member.Id, cancellationToken);
                    memberResults.Add(new CommunityMemberResult
                    {
                        Id = id,
                        Label = member.label ?? id,
                        Type = member.kind ?? "unknown",
                        FilePath = member.filePath,
                        Degree = connections.Count
                    });
                }

                return JsonSerializer.Serialize(new CommunityDetailResult
                {
                    CommunityId = communityId.Value,
                    MemberCount = memberResults.Count,
                    Members = [.. memberResults.OrderByDescending(m => m.Degree)]
                }, JsonOptions);
            }

            // List all communities
            var listResponse = await _db.RawQuery(
                "SELECT community, count() AS count FROM entity WHERE community != NONE GROUP BY community ORDER BY count DESC",
                cancellationToken: cancellationToken
            );

            // Also get total counts
            var countResponse = await _db.RawQuery(
                "SELECT count() AS total FROM entity; SELECT count() AS total FROM entity WHERE community != NONE",
                cancellationToken: cancellationToken
            );

            int totalNodes = 0;
            int nodesInCommunities = 0;

            if (countResponse.FirstOk is { } countOk)
            {
                var totals = countOk.GetValues<SurrealCommunityCount>().ToList();
                // First query: SELECT count() AS total FROM entity
                if (countResponse.Count > 0 && countResponse[0] is SurrealDbOkResult r0)
                    totalNodes = r0.GetValues<SurrealCommunityCount>().FirstOrDefault()?.total ?? 0;
                // Second query: SELECT count() AS total FROM entity WHERE community != NONE
                if (countResponse.Count > 1 && countResponse[1] is SurrealDbOkResult r1)
                    nodesInCommunities = r1.GetValues<SurrealCommunityCount>().FirstOrDefault()?.total ?? 0;
            }

            var communities = new List<CommunitySummaryResult>();

            if (listResponse.FirstOk is { } listOk)
            {
                var communityGroups = listOk.GetValues<SurrealCommunityGroup>().ToList();

                foreach (var group in communityGroups)
                {
                    // Fetch top 5 members for each community
                    var memberParams = new Dictionary<string, object?>
                    {
                        ["communityId"] = group.community
                    };
                    var memberResponse = await _db.RawQuery(
                        "SELECT * FROM entity WHERE community = $communityId LIMIT 5",
                        memberParams,
                        cancellationToken
                    );

                    var topMembers = new List<CommunityMemberResult>();
                    if (memberResponse.FirstOk is { } memberOk)
                    {
                        foreach (var m in memberOk.GetValues<SurrealEntityRecord>())
                        {
                            var mid = ExtractNodeId(m.Id);
                            var conns = await FetchConnectionsAsync(m.Id, cancellationToken);
                            topMembers.Add(new CommunityMemberResult
                            {
                                Id = mid,
                                Label = m.label ?? mid,
                                Type = m.kind ?? "unknown",
                                Degree = conns.Count
                            });
                        }
                    }

                    communities.Add(new CommunitySummaryResult
                    {
                        CommunityId = group.community,
                        MemberCount = group.count,
                        TopMembers = topMembers
                    });
                }
            }

            return JsonSerializer.Serialize(new CommunitiesListResult
            {
                TotalCommunities = communities.Count,
                NodesInCommunities = nodesInCommunities,
                NodesWithoutCommunity = totalNodes - nodesInCommunities,
                Communities = communities
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Communities failed: {ex.Message}" });
        }
    }

    public async Task<string> AnalyzeAsync(int topN, CancellationToken cancellationToken = default)
    {
        try
        {
            // Run all aggregate queries in one batch
            var response = await _db.RawQuery(
                """
                SELECT count() AS count FROM entity;
                SELECT count() AS count FROM relationship;
                SELECT community, count() AS count FROM entity WHERE community != NONE GROUP BY community;
                SELECT kind, count() AS count FROM entity GROUP BY kind ORDER BY count DESC;
                SELECT type, count() AS count FROM relationship GROUP BY type ORDER BY count DESC;
                """,
                cancellationToken: cancellationToken
            );

            if (response.HasErrors)
                return JsonSerializer.Serialize(new ErrorResult { Error = "Analyze query failed" });

            // Result 0: total node count
            var nodeCount = response.GetValues<SurrealCount>(0).FirstOrDefault()?.count ?? 0;
            // Result 1: total edge count
            var edgeCount = response.GetValues<SurrealCount>(1).FirstOrDefault()?.count ?? 0;
            // Result 2: community groups
            var communityGroups = response.GetValues<SurrealCommunityGroup>(2).ToList();
            // Result 3: type distribution
            var typeDistributions = response.GetValues<SurrealKindCount>(3).ToList();
            // Result 4: relationship type distribution
            var relTypeDistributions = response.GetValues<SurrealTypeCount>(4).ToList();

            // Top N nodes by degree
            var topNodes = await GetTopNodesAsync(topN, cancellationToken);

            // Isolated nodes (degree == 0)
            var isolatedCount = await CountIsolatedNodesAsync(cancellationToken);

            double averageDegree = nodeCount > 0 ? (double)edgeCount * 2 / nodeCount : 0;

            return JsonSerializer.Serialize(new AnalyzeResult
            {
                Statistics = new AnalyzeStatistics
                {
                    NodeCount = nodeCount,
                    EdgeCount = edgeCount,
                    CommunityCount = communityGroups.Count,
                    AverageDegree = Math.Round(averageDegree, 2),
                    IsolatedNodeCount = isolatedCount
                },
                TopNodes = topNodes,
                NodeTypes = typeDistributions.Select(t => new TypeCountResult
                {
                    Type = t.kind ?? "unknown",
                    Count = t.count
                }).ToList(),
                RelationshipTypes = relTypeDistributions.Select(t => new TypeCountResult
                {
                    Type = t.type ?? "unknown",
                    Count = t.count
                }).ToList()
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Analyze failed: {ex.Message}" });
        }
    }

    // ── Private helpers ────────────────────────────────────────────────

    private static string ExtractNodeId(RecordId? recordId)
    {
        if (recordId is null)
            return "";

        try
        {
            var escaped = recordId.DeserializeId<string>();
            return Uri.UnescapeDataString(escaped);
        }
        catch
        {
            return recordId.ToString() ?? "";
        }
    }

    private static RecordId MakeEntityRecordId(string nodeId)
    {
        var escaped = Uri.EscapeDataString(nodeId);
        return (RecordId)("entity", escaped);
    }

    private async Task<List<ConnectionResult>> FetchConnectionsAsync(RecordId? entityId, CancellationToken ct)
    {
        if (entityId is null)
            return [];

        var parameters = new Dictionary<string, object?>
        {
            ["record_id"] = entityId
        };

        var response = await _db.RawQuery(
            "SELECT * FROM relationship WHERE source = $record_id OR target = $record_id LIMIT 50",
            parameters,
            ct
        );

        if (response.FirstOk is not { } ok)
            return [];

        return ok.GetValues<SurrealRelationshipRecord>().Select(rel => new ConnectionResult
        {
            Source = ExtractNodeId(rel.source),
            Target = ExtractNodeId(rel.target),
            Relationship = rel.type ?? "",
            Weight = rel.weight
        }).ToList();
    }

    private async Task EnsurePathCacheLoadedAsync(CancellationToken ct)
    {
        if (_entitiesCache != null)
            return;

        // Fetch all entities and relationships
        var response = await _db.RawQuery(
            "SELECT * FROM entity; SELECT * FROM relationship",
            cancellationToken: ct
        );

        if (response.HasErrors)
            return;

        lock (_cacheLock)
        {
            if (_entitiesCache != null)
                return;

            var entities = new Dictionary<string, SurrealEntityRecord>();
            if (response.Count > 0 && response[0] is SurrealDbOkResult entityOk)
            {
                foreach (var e in entityOk.GetValues<SurrealEntityRecord>())
                {
                    var id = ExtractNodeId(e.Id);
                    if (!string.IsNullOrEmpty(id))
                        entities[id] = e;
                }
            }

            var relationships = new List<SurrealRelationshipRecord>();
            if (response.Count > 1 && response[1] is SurrealDbOkResult relOk)
            {
                relationships.AddRange(relOk.GetValues<SurrealRelationshipRecord>());
            }

            _entitiesCache = entities;
            _relationshipsCache = relationships;
        }
    }

    private async Task<List<AnalyzeNodeResult>> GetTopNodesAsync(int topN, CancellationToken ct)
    {
        // Load all entities and compute degree by counting relationships
        var response = await _db.RawQuery(
            "SELECT * FROM entity",
            cancellationToken: ct
        );

        if (response.FirstOk is not { } ok)
            return [];

        var entities = ok.GetValues<SurrealEntityRecord>().ToList();

        // Compute degree for each entity
        var degreeMap = new Dictionary<string, int>();
        foreach (var entity in entities)
        {
            var id = ExtractNodeId(entity.Id);
            var connections = await FetchConnectionsAsync(entity.Id, ct);
            degreeMap[id] = connections.Count;
        }

        return entities
            .Select(e => new
            {
                Id = ExtractNodeId(e.Id),
                Label = e.label ?? "",
                Type = e.kind ?? "unknown",
                Degree = degreeMap.GetValueOrDefault(ExtractNodeId(e.Id), 0),
                Community = e.community
            })
            .OrderByDescending(x => x.Degree)
            .Take(topN)
            .Select(x => new AnalyzeNodeResult
            {
                Id = x.Id,
                Label = x.Label,
                Type = x.Type,
                Degree = x.Degree,
                Community = x.Community
            })
            .ToList();
    }

    private async Task<int> CountIsolatedNodesAsync(CancellationToken ct)
    {
        var response = await _db.RawQuery(
            "SELECT * FROM entity",
            cancellationToken: ct
        );

        if (response.FirstOk is not { } ok)
            return 0;

        var entities = ok.GetValues<SurrealEntityRecord>().ToList();
        int isolated = 0;

        foreach (var entity in entities)
        {
            var connections = await FetchConnectionsAsync(entity.Id, ct);
            if (connections.Count == 0)
                isolated++;
        }

        return isolated;
    }

    // ── Internal SurrealDB record types for CBOR deserialization ──────

    internal sealed class SurrealEntityRecord : Record
    {
        public string? label { get; set; }
        public string? kind { get; set; }
        public string? filePath { get; set; }
        public string? language { get; set; }
        public string? confidence { get; set; }
        public int? community { get; set; }
    }

    internal sealed class SurrealRelationshipRecord : Record
    {
        public RecordId? source { get; set; }
        public RecordId? target { get; set; }
        public string? type { get; set; }
        public double weight { get; set; }
        public string? confidence { get; set; }
    }

    // Aggregation result types
    internal sealed class SurrealCount : Record
    {
        public int count { get; set; }
    }

    internal sealed class SurrealCommunityGroup : Record
    {
        public int community { get; set; }
        public int count { get; set; }
    }

    internal sealed class SurrealCommunityCount : Record
    {
        public int total { get; set; }
    }

    internal sealed class SurrealKindCount : Record
    {
        public string? kind { get; set; }
        public int count { get; set; }
    }

    internal sealed class SurrealTypeCount : Record
    {
        public string? type { get; set; }
        public int count { get; set; }
    }
}
