using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace RagShared;

public static class McpSearch
{
    private static readonly JsonSerializerOptions RowOptions = new()
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    public static async Task<McpClient> ConnectAsync(CancellationToken cancellationToken = default)
    {
        string endpoint = Environment.GetEnvironmentVariable("TOOLBOX_MCP_ENDPOINT")
            ?? "http://localhost:15000/mcp/rag";
        HttpClientTransport transport = new(new HttpClientTransportOptions
        {
            Name = "RAG Toolbox",
            Endpoint = new Uri(endpoint),
            TransportMode = HttpTransportMode.StreamableHttp
        });
        return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
    }

    public static Task<IReadOnlyList<SearchHit>> KeywordsAsync(
        McpClient client, string query, int top = 3, CancellationToken cancellationToken = default) =>
        CallAsync(client, "search_keywords", new() { ["query"] = query, ["top"] = top }, cancellationToken);

    public static async Task<IReadOnlyList<Document>> GetDocumentsAsync(
        McpClient client, CancellationToken cancellationToken = default)
    {
        CallToolResult result = await client.CallToolAsync("list_documents", cancellationToken: cancellationToken);
        return ReadDocuments(result);
    }

    public static Task<IReadOnlyList<SearchHit>> GraphAsync(
        McpClient client, string entity, int top = 3, CancellationToken cancellationToken = default) =>
        CallAsync(client, "search_graph", new() { ["entity"] = entity, ["top"] = top }, cancellationToken);

    private static async Task<IReadOnlyList<SearchHit>> CallAsync(
        McpClient client, string name, Dictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        CallToolResult result = await client.CallToolAsync(name, arguments, cancellationToken: cancellationToken);
        return ReadHits(result, allowNullResult: name == "search_graph");
    }

    public static IReadOnlyList<SearchHit> ReadHits(CallToolResult result, bool allowNullResult = false) =>
        ReadRows<SearchHit>(result, allowNullResult);

    public static IReadOnlyList<Document> ReadDocuments(CallToolResult result) => ReadRows<Document>(result);

    private static IReadOnlyList<T> ReadRows<T>(CallToolResult result, bool allowNullResult = false) where T : class
    {
        if (result.IsError == true)
        {
            string error = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
            throw new InvalidOperationException($"MCP retrieval failed: {error}");
        }

        List<T> rows = [];
        // Toolbox emits complete JSON rows as text blocks (or an array for a batched result).
        foreach (ContentBlock block in result.Content)
        {
            if (block is not TextContentBlock text)
            {
                throw new JsonException("Expected JSON text from a Toolbox database tool.");
            }

            using JsonDocument document = JsonDocument.Parse(text.Text);
            // Toolbox 1.13.1's Neo4j connector represents an empty result as a single JSON null.
            if (allowNullResult && result.Content.Count == 1 && document.RootElement.ValueKind == JsonValueKind.Null)
            {
                return [];
            }

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement row in document.RootElement.EnumerateArray())
                {
                    rows.Add(ReadRow<T>(row));
                }
            }
            else
            {
                rows.Add(ReadRow<T>(document.RootElement));
            }
        }

        return rows;
    }

    private static T ReadRow<T>(JsonElement row) where T : class =>
        row.ValueKind == JsonValueKind.Object
            ? row.Deserialize<T>(RowOptions) ?? throw new JsonException("Expected a non-null Toolbox row.")
            : throw new JsonException("Expected a JSON object for each Toolbox row.");
}
