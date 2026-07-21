using System.Text.Json;
using Graphify.Cli.Mcp;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Mcp;

[Trait("Category", "Mcp")]
public sealed class MemoryGraphBackendTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static KnowledgeGraph CreateSampleGraph()
    {
        var graph = new KnowledgeGraph();
        var n1 = new GraphNode { Id = "ClassA", Label = "ClassA", Type = "class", FilePath = "a.cs", Language = "csharp", Confidence = Confidence.Extracted };
        var n2 = new GraphNode { Id = "ClassB", Label = "ClassB", Type = "class", FilePath = "b.cs", Language = "csharp", Confidence = Confidence.Extracted };
        var n3 = new GraphNode { Id = "Helper", Label = "Helper", Type = "module", FilePath = "helper.cs", Language = "csharp", Confidence = Confidence.Extracted };
        var n4 = new GraphNode { Id = "Util", Label = "Utility", Type = "module", FilePath = "util.cs", Language = "csharp", Confidence = Confidence.Extracted, Community = 1 };

        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddNode(n3);
        graph.AddNode(n4);

        graph.AddEdge(new GraphEdge { Source = n1, Target = n2, Relationship = "calls", Weight = 1.0, Confidence = Confidence.Extracted });
        graph.AddEdge(new GraphEdge { Source = n2, Target = n3, Relationship = "imports", Weight = 1.0, Confidence = Confidence.Extracted });
        graph.AddEdge(new GraphEdge { Source = n1, Target = n4, Relationship = "uses", Weight = 0.5, Confidence = Confidence.Extracted });

        return graph;
    }

    [Fact]
    public async Task QueryAsync_WithMatchingTerm_ReturnsResults()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.QueryAsync("Class", 10);
        var result = JsonSerializer.Deserialize<QueryResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal("Class", result.Query);
        Assert.Equal(2, result.ResultCount);
        Assert.All(result.Results, r => Assert.Contains("Class", r.Id));
    }

    [Fact]
    public async Task QueryAsync_WithNoMatch_ReturnsEmpty()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.QueryAsync("ZzzNotFound", 10);
        var result = JsonSerializer.Deserialize<QueryResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task QueryAsync_WithEmptyTerm_ReturnsError()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.QueryAsync("", 10);
        var error = JsonSerializer.Deserialize<ErrorResult>(json, JsonOptions);

        Assert.NotNull(error);
        Assert.NotEmpty(error.Error);
    }

    [Fact]
    public async Task QueryAsync_RespectsLimit()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.QueryAsync("a", 1);
        var result = JsonSerializer.Deserialize<QueryResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Results.Count <= 1);
    }

    [Fact]
    public async Task PathAsync_FindsShortestPath()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.PathAsync("ClassA", "Helper");
        var result = JsonSerializer.Deserialize<PathResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Found);
        Assert.Equal(2, result.PathLength);
        Assert.NotNull(result.Path);
        Assert.Equal("ClassA", result.Path[0].Id);
        Assert.Equal("Helper", result.Path[^1].Id);
    }

    [Fact]
    public async Task PathAsync_WhenNoPath_ReturnsNotFound()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "A", Label = "A", Type = "node", FilePath = "a.cs", Language = "csharp", Confidence = Confidence.Extracted });
        graph.AddNode(new GraphNode { Id = "B", Label = "B", Type = "node", FilePath = "b.cs", Language = "csharp", Confidence = Confidence.Extracted });
        var backend = new MemoryGraphBackend(graph);

        var json = await backend.PathAsync("A", "B");
        var result = JsonSerializer.Deserialize<PathResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.False(result.Found);
    }

    [Fact]
    public async Task PathAsync_WithMissingNode_ReturnsError()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.PathAsync("NonExistent", "ClassA");
        var error = JsonSerializer.Deserialize<ErrorResult>(json, JsonOptions);

        Assert.NotNull(error);
        Assert.NotEmpty(error.Error);
    }

    [Fact]
    public async Task ExplainAsync_ReturnsNodeDetails()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.ExplainAsync("ClassA");
        var result = JsonSerializer.Deserialize<ExplainResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal("ClassA", result.Node.Id);
        Assert.Equal("class", result.Node.Type);
        Assert.Equal("a.cs", result.Node.FilePath);
        Assert.NotNull(result.Statistics);
        Assert.True(result.Statistics.TotalDegree > 0);
    }

    [Fact]
    public async Task ExplainAsync_WithMissingNode_ReturnsError()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.ExplainAsync("NonExistent");
        var error = JsonSerializer.Deserialize<ErrorResult>(json, JsonOptions);

        Assert.NotNull(error);
        Assert.NotEmpty(error.Error);
    }

    [Fact]
    public async Task CommunitiesAsync_ListsAllCommunities()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.CommunitiesAsync(null);
        var result = JsonSerializer.Deserialize<CommunitiesListResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.True(result.TotalCommunities > 0);
        Assert.Contains(result.Communities, c => c.CommunityId == 1);
    }

    [Fact]
    public async Task CommunitiesAsync_WithSpecificId_ReturnsMembers()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.CommunitiesAsync(1);
        var result = JsonSerializer.Deserialize<CommunityDetailResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(1, result.CommunityId);
        Assert.Single(result.Members);
        Assert.Equal("Util", result.Members[0].Id);
    }

    [Fact]
    public async Task CommunitiesAsync_WithMissingId_ReturnsError()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.CommunitiesAsync(999);
        var error = JsonSerializer.Deserialize<ErrorResult>(json, JsonOptions);

        Assert.NotNull(error);
        Assert.NotEmpty(error.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsGraphStats()
    {
        var backend = new MemoryGraphBackend(CreateSampleGraph());

        var json = await backend.AnalyzeAsync(5);
        var result = JsonSerializer.Deserialize<AnalyzeResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.Statistics);
        Assert.Equal(4, result.Statistics.NodeCount);
        Assert.Equal(3, result.Statistics.EdgeCount);
        Assert.NotNull(result.TopNodes);
        Assert.NotEmpty(result.NodeTypes);
        Assert.NotEmpty(result.RelationshipTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyGraph_ReturnsError()
    {
        var backend = new MemoryGraphBackend(new KnowledgeGraph());

        var json = await backend.AnalyzeAsync(10);
        var error = JsonSerializer.Deserialize<ErrorResult>(json, JsonOptions);

        Assert.NotNull(error);
        Assert.NotEmpty(error.Error);
    }

    [Fact]
    public async Task AnalyzeAsync_TopNodes_ReturnsHighestDegree()
    {
        var graph = new KnowledgeGraph();
        var hub = new GraphNode { Id = "Hub", Label = "Hub", Type = "class", FilePath = "hub.cs", Language = "csharp", Confidence = Confidence.Extracted };
        graph.AddNode(hub);
        for (int i = 0; i < 5; i++)
        {
            var n = new GraphNode { Id = $"N{i}", Label = $"N{i}", Type = "class", FilePath = "n.cs", Language = "csharp", Confidence = Confidence.Extracted };
            graph.AddNode(n);
            graph.AddEdge(new GraphEdge { Source = hub, Target = n, Relationship = "connects", Weight = 1.0, Confidence = Confidence.Extracted });
        }
        var backend = new MemoryGraphBackend(graph);

        var json = await backend.AnalyzeAsync(3);
        var result = JsonSerializer.Deserialize<AnalyzeResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(6, result.Statistics.NodeCount);
        Assert.Equal(5, result.Statistics.EdgeCount);
        Assert.Equal("Hub", result.TopNodes[0].Id);
        Assert.Equal(5, result.TopNodes[0].Degree);
    }
}
