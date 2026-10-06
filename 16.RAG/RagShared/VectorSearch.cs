using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Npgsql;
using Pgvector;
using Pgvector.Npgsql;

namespace RagShared;

public sealed class VectorSearch : IAsyncDisposable
{
    public const int Dimensions = 1536;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly string _embeddingModel;

    public VectorSearch(string connectionString,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, string embeddingModel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingModel);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseVector();
        _dataSource = builder.Build();
        _embeddingGenerator = embeddingGenerator;
        _embeddingModel = embeddingModel;
    }

    public async Task<int> IndexAsync(IReadOnlyList<Document> documents,
        CancellationToken cancellationToken = default)
    {
        if (documents.Select(document => document.Id).Distinct(StringComparer.Ordinal).Count() != documents.Count)
        {
            throw new ArgumentException("Document IDs must be unique within an ingestion batch.", nameof(documents));
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using (var schema = new NpgsqlCommand("""
            CREATE EXTENSION IF NOT EXISTS vector;
            CREATE TABLE IF NOT EXISTS rag_sample_vector_settings (
                singleton boolean PRIMARY KEY CHECK (singleton),
                model text NOT NULL,
                dimensions integer NOT NULL CHECK (dimensions = 1536)
            );
            CREATE TABLE IF NOT EXISTS rag_sample_vectors (
                id text PRIMARY KEY,
                title text NOT NULL,
                body text NOT NULL,
                content_hash text NOT NULL,
                embedding vector(1536) NOT NULL
            );
            """, connection))
        {
            await schema.ExecuteNonQueryAsync(cancellationToken);
        }
        await connection.ReloadTypesAsync();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var settings = new NpgsqlCommand("""
            INSERT INTO rag_sample_vector_settings (singleton, model, dimensions)
            VALUES (true, $1, 1536) ON CONFLICT (singleton) DO NOTHING;
            """, connection, transaction))
        {
            settings.Parameters.AddWithValue(_embeddingModel);
            await settings.ExecuteNonQueryAsync(cancellationToken);
        }
        // Serialize ingestion batches; queries see only committed document/vector pairs.
        await CheckSettingsAsync(connection, transaction, cancellationToken);

        var changed = new List<(Document Document, string Hash)>();
        foreach (var document in documents)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document))));
            await using var existing = new NpgsqlCommand(
                "SELECT content_hash FROM rag_sample_vectors WHERE id = $1", connection, transaction);
            existing.Parameters.AddWithValue(document.Id);
            if ((string?)await existing.ExecuteScalarAsync(cancellationToken) != hash)
            {
                changed.Add((document, hash));
            }
        }

        if (changed.Count > 0)
        {
            var embeddings = await _embeddingGenerator.GenerateAsync(
                changed.Select(item => $"{item.Document.Service}\n{item.Document.Title}\n{item.Document.Text}"),
                new EmbeddingGenerationOptions { Dimensions = Dimensions }, cancellationToken);
            if (embeddings.Count != changed.Count)
            {
                throw new InvalidOperationException("The embedding count must match the ingestion batch.");
            }
            for (var i = 0; i < changed.Count; i++)
            {
                CheckVector(embeddings[i].Vector);
                var (document, hash) = changed[i];
                await using var upsert = new NpgsqlCommand("""
                    INSERT INTO rag_sample_vectors (id, title, body, content_hash, embedding)
                    VALUES ($1, $2, $3, $4, $5)
                    ON CONFLICT (id) DO UPDATE SET title = EXCLUDED.title, body = EXCLUDED.body,
                        content_hash = EXCLUDED.content_hash, embedding = EXCLUDED.embedding;
                    """, connection, transaction);
                upsert.Parameters.AddWithValue(document.Id);
                upsert.Parameters.AddWithValue(document.Title);
                upsert.Parameters.AddWithValue(document.Text);
                upsert.Parameters.AddWithValue(hash);
                upsert.Parameters.AddWithValue(new Vector(embeddings[i].Vector));
                await upsert.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return changed.Count;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query, int top = 3, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await CheckSettingsAsync(connection, null, cancellationToken);
        var vector = await _embeddingGenerator.GenerateVectorAsync(query,
            new EmbeddingGenerationOptions { Dimensions = Dimensions }, cancellationToken);
        CheckVector(vector);
        // No ANN index: an exact cosine scan is appropriate for eight documents.
        await using var command = new NpgsqlCommand("""
            SELECT id, title, body, 1 - (embedding <=> $1) AS score
            FROM rag_sample_vectors
            ORDER BY embedding <=> $1, id
            LIMIT $2;
            """, connection);
        command.Parameters.AddWithValue(new Vector(vector));
        command.Parameters.AddWithValue(top);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var hits = new List<SearchHit>();
        while (await reader.ReadAsync(cancellationToken))
        {
            hits.Add(new SearchHit { Id = reader.GetString(0), Title = reader.GetString(1), Text = reader.GetString(2), Score = reader.GetDouble(3), Source = "vector" });
        }
        return hits;
    }

    private async Task CheckSettingsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT model, dimensions FROM rag_sample_vector_settings WHERE singleton = true
            """ + (transaction is null ? "" : " FOR UPDATE"), connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetString(0) != _embeddingModel ||
            reader.GetInt32(1) != Dimensions)
        {
            throw new InvalidOperationException(
                "Embedding model/dimension mismatch. Use the original model or a new, empty sample database.");
        }
    }

    public static void CheckVector(ReadOnlyMemory<float> vector)
    {
        if (vector.Length != Dimensions)
        {
            throw new InvalidOperationException($"Expected {Dimensions} dimensions; got {vector.Length}.");
        }
        if (vector.Span.ContainsAnyExcept(0f) == false || vector.ToArray().Any(value => !float.IsFinite(value)))
        {
            throw new InvalidOperationException("Cosine search requires a finite, nonzero vector.");
        }
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
