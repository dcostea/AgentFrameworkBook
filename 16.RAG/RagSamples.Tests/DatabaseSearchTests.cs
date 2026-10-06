using ModelContextProtocol.Client;
using RagShared;
using Xunit;

namespace RagSamples.Tests;

public sealed class DatabaseFactAttribute : FactAttribute
{
    internal static string? DisabledReason => Environment.GetEnvironmentVariable("RUN_DATABASE_TESTS") == "1"
        ? null
        : "Set RUN_DATABASE_TESTS=1 with seeded databases and Toolbox running. TOOLBOX_MCP_ENDPOINT overrides http://localhost:15000/mcp/rag. Tests never start or seed services.";

    public DatabaseFactAttribute() => Skip = DisabledReason;
}

public sealed class DatabaseTheoryAttribute : TheoryAttribute
{
    public DatabaseTheoryAttribute() => Skip = DatabaseFactAttribute.DisabledReason;
}

[Trait("Category", "Database")]
public sealed class DatabaseSearchTests
{
    [DatabaseFact]
    public async Task Keywords_CalibrationMatchesMotorAndUnknownTermReturnsEmpty()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        IReadOnlyList<SearchHit> hits = await McpSearch.KeywordsAsync(client, "calibration", 10, timeout.Token);

        SearchHit motor = Assert.Single(hits);
        Assert.Equal("doc-2", motor.Id);
        Assert.Equal("Wheel calibration records", motor.Title);
        Assert.Equal("keyword", motor.Source);
        Assert.True(motor.Score > 0);
        Assert.Contains("seven days", motor.Text);
        Assert.Empty(await McpSearch.KeywordsAsync(client, "zzzxqvnonexistent93", 10, timeout.Token));
    }

    [DatabaseFact]
    public async Task Keywords_PunctuationAndSqlLikeText_AreQueriesNotCommands()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        Assert.Equal("doc-2", Assert.Single(await McpSearch.KeywordsAsync(client, "calibration!!!", 10, timeout.Token)).Id);
        Assert.Empty(await McpSearch.KeywordsAsync(client, "' OR 1=1 --", 10, timeout.Token));
        Assert.Equal("doc-2", Assert.Single(await McpSearch.KeywordsAsync(client, "calibration", 10, timeout.Token)).Id);
    }

    [DatabaseFact]
    public async Task Bm25_CalibrationMatchesMotorAndUnknownTermReturnsEmpty()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        SearchHit motor = Assert.Single(await SearchBm25Async(client, "calibration", 10, timeout.Token));

        Assert.Equal("doc-2", motor.Id);
        Assert.Equal("Wheel calibration records", motor.Title);
        Assert.Equal("bm25", motor.Source);
        Assert.True(double.IsFinite(motor.Score) && motor.Score > 0);
        Assert.Contains("seven days", motor.Text);
        Assert.Null(motor.Evidence);
        Assert.Empty(await SearchBm25Async(client, "zzzxqvnonexistent93", 10, timeout.Token));
    }

    [DatabaseFact]
    public async Task Bm25_PartialMatchesUseAllRowsRatherThanAnAndPrefilter()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        IReadOnlyList<SearchHit> hits = await SearchBm25Async(client, "calibration suspicious", 10, timeout.Token);

        Assert.Equal(["doc-2", "doc-3"], hits.Select(hit => hit.Id).Order(StringComparer.Ordinal));
        Assert.All(hits, hit =>
        {
            Assert.Equal("bm25", hit.Source);
            Assert.True(double.IsFinite(hit.Score) && hit.Score > 0);
        });
        Assert.Equal(hits.Take(1), await SearchBm25Async(client, "calibration suspicious", 1, timeout.Token));
    }

    [DatabaseFact]
    public async Task Bm25_PunctuationAndSqlLikeText_AreTokensNotCommands()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        Assert.Equal("doc-2", Assert.Single(await SearchBm25Async(client, "calibration!!!", 10, timeout.Token)).Id);
        // "or" is a real word in these documents, not a Boolean operator.
        Assert.Equal(["doc-3", "doc-8"], (await SearchBm25Async(client, "' OR 1=1 --", 10, timeout.Token))
            .Select(hit => hit.Id).Order(StringComparer.Ordinal));
        Assert.Empty(await SearchBm25Async(client, "'; DROP TABLE rag_sample_documents; --", 10, timeout.Token));
        Assert.Equal("doc-2", Assert.Single(await SearchBm25Async(client, "calibration", 10, timeout.Token)).Id);
    }

    private static async Task<IReadOnlyList<SearchHit>> SearchBm25Async(
        McpClient client, string query, int top, CancellationToken cancellationToken)
    {
        IReadOnlyList<Document> documents = await McpSearch.GetDocumentsAsync(client, cancellationToken);
        return Bm25Search.Search(documents, query, top);
    }

    [DatabaseFact]
    public async Task Graph_Safety_ReturnsInboundDependenciesWithActualEvidenceAndStopsAtTwoHops()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        IReadOnlyList<SearchHit> hits = await McpSearch.GraphAsync(client, "SafetyService", 10, timeout.Token);

        Assert.Equal(["doc-3", "doc-2", "doc-1"], hits.Select(hit => hit.Id));
        Assert.All(hits, hit => Assert.Equal("graph", hit.Source));
        Assert.Equal(1, hits[0].Score);
        Assert.Equal(0.5, hits[1].Score);
        Assert.Equal(1.0 / 3, hits[2].Score, 12);
        Assert.Equal("MotorService -> SafetyService (DEPENDS_ON; 1 hops)", hits[1].Evidence);
        Assert.Equal("NavigationService -> MotorService -> SafetyService (DEPENDS_ON; 2 hops)", hits[2].Evidence);
        Assert.DoesNotContain(hits, hit => hit.Id == "doc-8");
    }

    [DatabaseFact]
    public async Task Graph_UnknownEntityOrCypherLikeText_ReturnsEmpty()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        Assert.Empty(await McpSearch.GraphAsync(client, "Unknown Service", 10, timeout.Token));
        Assert.Empty(await McpSearch.GraphAsync(client, "' OR true //", 10, timeout.Token));
    }
}
