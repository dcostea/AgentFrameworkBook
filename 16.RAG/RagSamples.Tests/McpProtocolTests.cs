using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using RagShared;
using Xunit;

namespace RagSamples.Tests;

[Trait("Category", "Database")]
public sealed class McpProtocolTests
{
    [DatabaseFact]
    public async Task HttpToolbox_AdvertisesOnlyTheThreeConfiguredReadTools()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: timeout.Token);

        Assert.Equal(["list_documents", "search_graph", "search_keywords"],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        foreach ((string name, string input) in new[] { ("search_keywords", "query"), ("search_graph", "entity") })
        {
            McpClientTool tool = Assert.Single(tools, candidate => candidate.Name == name);
            JsonElement schema = tool.JsonSchema;
            Assert.Equal("object", schema.GetProperty("type").GetString());
            JsonElement properties = schema.GetProperty("properties");
            Assert.Equal(new[] { input, "top" }.Order(), properties.EnumerateObject().Select(property => property.Name).Order());
            Assert.Equal("string", properties.GetProperty(input).GetProperty("type").GetString());
            Assert.Equal("integer", properties.GetProperty("top").GetProperty("type").GetString());
            Assert.Equal(new[] { input, "top" }.Order(), schema.GetProperty("required").EnumerateArray()
                .Select(value => value.GetString()).Order());
        }
    }

    [DatabaseTheory]
    [InlineData("search_keywords", "query")]
    [InlineData("search_graph", "entity")]
    public async Task HttpToolbox_BlankSearches_ReturnNoRows(string name, string input)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        CallToolResult result = await client.CallToolAsync(name,
            new Dictionary<string, object?> { [input] = "", ["top"] = 3 }, cancellationToken: timeout.Token);

        Assert.False(result.IsError ?? false);
        Assert.Empty(McpSearch.ReadHits(result, allowNullResult: name == "search_graph"));
        IReadOnlyList<SearchHit> whitespaceHits = name == "search_keywords"
            ? await McpSearch.KeywordsAsync(client, " \t\n", cancellationToken: timeout.Token)
            : await McpSearch.GraphAsync(client, " \t\n", cancellationToken: timeout.Token);
        Assert.Empty(whitespaceHits);
    }

    [DatabaseTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HttpToolbox_RejectsMissingOrIncorrectlyTypedArguments(bool missingQuery)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);
        Dictionary<string, object?> arguments = missingQuery
            ? new() { ["top"] = 3 }
            : new() { ["query"] = "", ["top"] = "not-an-integer" };

        CallToolResult result = await client.CallToolAsync("search_keywords", arguments, cancellationToken: timeout.Token);

        Assert.True(result.IsError);
        Assert.NotEmpty(Assert.Single(result.Content.OfType<TextContentBlock>()).Text);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => McpSearch.ReadHits(result));
        Assert.Contains("MCP retrieval failed", exception.Message);
    }

    [DatabaseFact]
    public async Task HttpToolbox_ListsTheCompleteCanonicalCorpusForLocalBm25()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await using McpClient client = await McpSearch.ConnectAsync(timeout.Token);

        IReadOnlyList<Document> documents = await McpSearch.GetDocumentsAsync(client, timeout.Token);

        Assert.Equal(8, documents.Count);
        Assert.Equal(SampleData.Documents.OrderBy(document => document.Id, StringComparer.Ordinal), documents);
        Assert.Equal(["doc-2", "doc-3"], Bm25Search.Search(documents, "calibration suspicious")
            .Select(hit => hit.Id).Order(StringComparer.Ordinal));
    }
}
