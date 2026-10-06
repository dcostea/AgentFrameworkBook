using System.Text.Json;
using ModelContextProtocol.Protocol;
using RagShared;
using Xunit;

namespace RagSamples.Tests;

public sealed class McpSearchParserTests
{
    [Fact]
    public void ReadHits_ToolboxRowBlocks_PreserveOrderAndEvidence()
    {
        CallToolResult result = new()
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = """
                        {"Id":"doc-2","Title":"Wheel calibration records","Text":"Calibration records expire after seven days.\nTechnician review required.","Score":0.75,"Source":"graph","Evidence":"MotorService -[DEPENDS_ON]-> SafetyService"}
                        """
                },
                new TextContentBlock
                {
                    Text = """
                        {"Id":"doc-1","Title":"Routes","Text":"Create routes.","Score":0.5,"Source":"keyword","Evidence":null}
                        """
                }
            ]
        };

        IReadOnlyList<SearchHit> hits = McpSearch.ReadHits(result);

        Assert.Equal(["doc-2", "doc-1"], hits.Select(hit => hit.Id));
        Assert.Equal("Wheel calibration records", hits[0].Title);
        Assert.Equal("Calibration records expire after seven days.\nTechnician review required.", hits[0].Text);
        Assert.Equal(0.75, hits[0].Score);
        Assert.Equal("graph", hits[0].Source);
        Assert.Equal("MotorService -[DEPENDS_ON]-> SafetyService", hits[0].Evidence);
        Assert.Equal("keyword", hits[1].Source);
        Assert.Null(hits[1].Evidence);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData(" \n[ ]\t")]
    public void ReadHits_EmptyJsonArray_ReturnsEmpty(string json)
    {
        Assert.Empty(McpSearch.ReadHits(Result(json)));
    }

    [Fact]
    public void ReadHits_ToolError_ThrowsWithServerDiagnostic()
    {
        CallToolResult result = Result("database unavailable");
        result.IsError = true;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => McpSearch.ReadHits(result));

        Assert.Contains("MCP retrieval failed", exception.Message);
        Assert.Contains("database unavailable", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not JSON")]
    [InlineData("[")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"Id\":\"doc-1\"}")]
    [InlineData("{\"Id\":null,\"Title\":\"T\",\"Text\":\"B\",\"Score\":1,\"Source\":\"keyword\"}")]
    public void ReadHits_InvalidRows_ThrowRatherThanPretendingNoMatches(string json)
    {
        Assert.ThrowsAny<JsonException>(() => McpSearch.ReadHits(Result(json)));
    }

    [Fact]
    public void ReadHits_EmptyContent_IsToolboxsEmptyResult()
    {
        Assert.Empty(McpSearch.ReadHits(new CallToolResult { Content = [] }));
    }

    [Fact]
    public void ReadHits_BatchedArray_ReturnsTypedRows()
    {
        CallToolResult result = Result("""
            [{"Id":"doc-2","Title":"Calibrations","Text":"Seven days.","Score":0.7,"Source":"keyword"}]
            """);

        SearchHit hit = Assert.Single(McpSearch.ReadHits(result));

        Assert.Equal("doc-2", hit.Id);
        Assert.Equal(0.7, hit.Score);
        Assert.Null(hit.Evidence);
    }

    [Fact]
    public void ReadHits_FragmentedJson_IsNotConcatenatedAcrossRows()
    {
        CallToolResult result = new()
        {
            Content = [new TextContentBlock { Text = "[" }, new TextContentBlock { Text = "]" }]
        };

        Assert.ThrowsAny<JsonException>(() => McpSearch.ReadHits(result));
    }

    [Fact]
    public void ReadDocuments_RowsHaveCompleteCorpusFields()
    {
        CallToolResult result = Result("""
            {"Id":"doc-2","Service":"MotorService","Title":"Calibrations","Text":"Seven days."}
            """);

        Document document = Assert.Single(McpSearch.ReadDocuments(result));

        Assert.Equal("doc-2", document.Id);
        Assert.Equal("MotorService", document.Service);
        Assert.Equal("Calibrations", document.Title);
        Assert.Equal("Seven days.", document.Text);
    }

    [Fact]
    public void ReadDocuments_MissingService_RejectsAnIncompleteBm25Corpus()
    {
        CallToolResult result = Result("""
            {"Id":"doc-2","Title":"Calibrations","Text":"Seven days."}
            """);

        Assert.Throws<JsonException>(() => McpSearch.ReadDocuments(result));
    }

    [Fact]
    public void ReadHits_Neo4jNullResult_IsExplicitlyAcceptedAsEmpty()
    {
        Assert.Empty(McpSearch.ReadHits(Result("null"), allowNullResult: true));
        Assert.Throws<JsonException>(() => McpSearch.ReadDocuments(Result("null")));
    }

    [Fact]
    public void ReadHits_NullAmongRows_IsNotSilentlyIgnored()
    {
        CallToolResult result = new()
        {
            Content = [new TextContentBlock { Text = "[]" }, new TextContentBlock { Text = "null" }]
        };

        Assert.Throws<JsonException>(() => McpSearch.ReadHits(result, allowNullResult: true));
        Assert.Throws<JsonException>(() => McpSearch.ReadHits(Result("[null]"), allowNullResult: true));
    }

    [Fact]
    public void ReadHits_Neo4jErrorIsNotAnEmptyResult()
    {
        CallToolResult result = Result("null");
        result.IsError = true;

        Assert.Throws<InvalidOperationException>(() => McpSearch.ReadHits(result, allowNullResult: true));
    }

    private static CallToolResult Result(string json) => new()
    {
        Content = [new TextContentBlock { Text = json }]
    };
}
