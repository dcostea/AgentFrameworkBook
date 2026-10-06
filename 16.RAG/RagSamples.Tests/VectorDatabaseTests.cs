using Npgsql;
using RagShared;
using Xunit;

namespace RagSamples.Tests;

[Trait("Category", "Database")]
public sealed class VectorDatabaseTests : IAsyncLifetime
{
    private readonly string _schema = "vector_test_" + Guid.NewGuid().ToString("N");
    private string _connectionString = "";

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("RAG_VECTOR_TEST_CONNECTION_STRING") ??
            throw new InvalidOperationException("Use an isolated local database and set RAG_VECTOR_TEST_CONNECTION_STRING.");
        var builder = new NpgsqlConnectionStringBuilder(configured);
        if (builder.Host is not ("localhost" or "127.0.0.1"))
        {
            throw new InvalidOperationException("Vector integration tests require a local database.");
        }
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE SCHEMA {_schema}", connection);
        await create.ExecuteNonQueryAsync();
        builder.SearchPath = $"{_schema},public";
        _connectionString = builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (_connectionString.Length == 0) return;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        // Only this test's randomly named schema is removed, never shared/public data.
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {_schema} CASCADE", connection);
        await drop.ExecuteNonQueryAsync();
    }

    [DatabaseFact]
    public async Task PersistentExactSearch_UpsertsOnlyChangesAndSurvivesReopening()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        Document[] documents =
        [
            new Document { Id = "a", Service = "MotorService", Title = "Calibration", Text = "original" },
            new Document { Id = "b", Service = "SafetyService", Title = "Review", Text = "second" },
            new Document { Id = "c", Service = "NavigationService", Title = "Routes", Text = "third" }
        ];
        using var generator = new TestEmbeddingGenerator(input =>
        {
            var vector = new float[VectorSearch.Dimensions];
            (vector[0], vector[1]) = input.Contains("original") ? (0.8f, 0.6f) :
                input.Contains("second") ? (0.6f, 0.8f) : input.Contains("third") ? (-1f, 0f) : (1f, 0f);
            return vector;
        });
        await using (var search = new VectorSearch(_connectionString, generator, "test-model"))
        {
            Assert.Equal(3, await search.IndexAsync(documents, timeout.Token));
            Assert.Equal(0, await search.IndexAsync(documents, timeout.Token));
            Assert.Single(generator.Calls);
        }
        await using var reopened = new VectorSearch(_connectionString, generator, "test-model");
        var hits = await reopened.SearchAsync("query", 10, timeout.Token);
        Assert.Equal(["a", "b", "c"], hits.Select(hit => hit.Id));
        Assert.Equal(0.8, hits[0].Score, 5);
        Assert.Equal(0.6, hits[1].Score, 5);
        Assert.Equal(-1, hits[2].Score, 5);
        Assert.All(hits, hit => Assert.Equal("vector", hit.Source));
        Assert.Equal("Calibration", hits[0].Title);
        Assert.Equal("original", hits[0].Text);
        Assert.Equal(hits.Take(1), await reopened.SearchAsync("query", 1, timeout.Token));

        documents[0] = documents[0] with { Title = "Updated calibration", Text = "updated" };
        Assert.Equal(1, await reopened.IndexAsync(documents, timeout.Token));
        Assert.Single(generator.Calls[^1]);
        var updated = await reopened.SearchAsync("query", 10, timeout.Token);
        Assert.Equal(3, updated.Count);
        Assert.Equal("a", updated[0].Id);
        Assert.Equal("Updated calibration", updated[0].Title);
        Assert.Equal("updated", updated[0].Text);
        Assert.Equal(1, updated[0].Score, 5);
        Assert.Equal(timeout.Token, generator.LastCancellationToken);
    }

    [DatabaseTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DimensionMismatch_FailsForIngestionAndQueryWithoutCorruptingRows(bool queryMismatch)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var invalid = false;
        using var generator = new TestEmbeddingGenerator(_ =>
        {
            if (invalid) return new float[2];
            var vector = new float[1536];
            vector[0] = 1;
            return vector;
        });
        await using var search = new VectorSearch(_connectionString, generator, "test-model");
        Document document = new Document { Id = "a", Service = "MotorService", Title = "Calibration", Text = "original" };
        Assert.Equal(1, await search.IndexAsync([document], timeout.Token));
        invalid = true;
        var error = queryMismatch
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => search.SearchAsync("query", cancellationToken: timeout.Token))
            : await Assert.ThrowsAsync<InvalidOperationException>(() => search.IndexAsync([document with { Text = "changed" }], timeout.Token));
        Assert.Contains("Expected 1536 dimensions; got 2", error.Message);
        invalid = false;
        Assert.Equal("original", Assert.Single(await search.SearchAsync("query", cancellationToken: timeout.Token)).Text);
    }

    [DatabaseFact]
    public async Task InvalidSecondEmbedding_RollsBackWholeBatchAndEqualScoresUseStableIds()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var generator = new TestEmbeddingGenerator(input =>
        {
            if (input.EndsWith("invalid", StringComparison.Ordinal)) return new float[2];
            var vector = new float[1536];
            vector[0] = 1;
            return vector;
        });
        Document[] documents =
        [
            new() { Id = "z", Service = "MotorService", Title = "Calibration", Text = "original-z" },
            new() { Id = "a", Service = "SafetyService", Title = "Review", Text = "original-a" }
        ];
        await using var search = new VectorSearch(_connectionString, generator, "test-model");
        await search.IndexAsync(documents, timeout.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => search.IndexAsync(
            [documents[0] with { Text = "changed" }, documents[1] with { Text = "invalid" }], timeout.Token));

        var hits = await search.SearchAsync("'; DROP TABLE rag_sample_vectors; --", 10, timeout.Token);
        Assert.Equal(["a", "z"], hits.Select(hit => hit.Id));
        Assert.Equal(["original-a", "original-z"], hits.Select(hit => hit.Text));
        Assert.All(hits, hit => Assert.Equal(1, hit.Score, 5));
        Assert.Equal(0, await search.IndexAsync(documents, timeout.Token));
        Assert.Equal(3, generator.Calls.Count);
    }

    [DatabaseFact]
    public async Task ModelMismatch_IsRejectedEvenWhenDimensionsMatch()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var generator = new TestEmbeddingGenerator(_ =>
        {
            var vector = new float[1536];
            vector[0] = 1;
            return vector;
        });
        await using var first = new VectorSearch(_connectionString, generator, "model-one");
        await first.IndexAsync([SampleData.Documents[0]], timeout.Token);
        await using var other = new VectorSearch(_connectionString, generator, "model-two");
        var indexError = await Assert.ThrowsAsync<InvalidOperationException>(() => other.IndexAsync([SampleData.Documents[0]], timeout.Token));
        var searchError = await Assert.ThrowsAsync<InvalidOperationException>(() => other.SearchAsync("query", cancellationToken: timeout.Token));
        Assert.Contains("model/dimension mismatch", indexError.Message);
        Assert.Contains("model/dimension mismatch", searchError.Message);
        Assert.Single(generator.Calls);
    }
}
