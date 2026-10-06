using RagShared;
using Xunit;

namespace RagSamples.Tests;

public sealed class VectorSearchTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(1535)]
    [InlineData(1537)]
    public void CheckVector_RejectsWrongDimensions(int dimensions)
    {
        var error = Assert.Throws<InvalidOperationException>(() => VectorSearch.CheckVector(new float[dimensions]));
        Assert.Contains($"Expected 1536 dimensions; got {dimensions}", error.Message);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void CheckVector_RejectsInvalidCosineInputs(float value)
    {
        var vector = new float[VectorSearch.Dimensions];
        vector[0] = value;
        Assert.Throws<InvalidOperationException>(() => VectorSearch.CheckVector(vector));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\n")]
    public async Task SearchAsync_BlankQueryDoesNotConnectOrEmbed(string query)
    {
        using var generator = new TestEmbeddingGenerator(_ => throw new InvalidOperationException());
        await using var search = new VectorSearch("Host=localhost;Database=unused", generator, "test-model");
        Assert.Empty(await search.SearchAsync(query));
        Assert.Empty(generator.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsync_NonpositiveTopDoesNotConnectOrEmbed(int top)
    {
        using var generator = new TestEmbeddingGenerator(_ => throw new InvalidOperationException());
        await using var search = new VectorSearch("Host=localhost;Database=unused", generator, "test-model");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => search.SearchAsync("calibration", top));
        Assert.Empty(generator.Calls);
    }

    [Fact]
    public async Task IndexAsync_DuplicateIdsFailBeforeConnecting()
    {
        using var generator = new TestEmbeddingGenerator(_ => throw new InvalidOperationException());
        await using var search = new VectorSearch("Host=localhost;Database=unused", generator, "test-model");
        await Assert.ThrowsAsync<ArgumentException>(() => search.IndexAsync(
            [SampleData.Documents[0], SampleData.Documents[0]]));
        Assert.Empty(generator.Calls);
    }
}
