using RagShared;
using Xunit;

namespace RagSamples.Tests;

public sealed class RankFusionTests
{
    [Fact]
    public void Fuse_IncompatibleRawScores_UsesRanksOnly()
    {
        IReadOnlyList<SearchHit> hits = RankFusion.Fuse(3,
            [Hit("a", -1000), Hit("b", 1_000_000), Hit("c", double.MaxValue)]);

        Assert.Equal(["a", "b", "c"], hits.Select(hit => hit.Id));
        Assert.Equal(1.0 / 61, hits[0].Score, 12);
        Assert.Equal(1.0 / 62, hits[1].Score, 12);
        Assert.Equal(1.0 / 63, hits[2].Score, 12);
    }

    [Fact]
    public void Fuse_OverlappingLowerRanks_OutrankSingleFirstPlaces()
    {
        IReadOnlyList<SearchHit> hits = RankFusion.Fuse(3,
            [Hit("a", 1000), Hit("b", 0.01)],
            [Hit("c", 9000), Hit("b", 0.001)]);

        Assert.Equal(["b", "a", "c"], hits.Select(hit => hit.Id));
        Assert.Equal(2.0 / 62, hits[0].Score, 12);
        Assert.Equal(1.0 / 61, hits[1].Score, 12);
    }

    [Fact]
    public void Fuse_TiedScores_UsesOrdinalIdRatherThanInputOrder()
    {
        IReadOnlyList<SearchHit> hits = RankFusion.Fuse(3,
            [Hit("z")], [Hit("a")], [Hit("B")]);

        Assert.Equal(["B", "a", "z"], hits.Select(hit => hit.Id));
        Assert.All(hits, hit => Assert.Equal(1.0 / 61, hit.Score, 12));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public void Fuse_Top_TruncatesWithoutPadding(int top)
    {
        IReadOnlyList<SearchHit> hits = RankFusion.Fuse(top, [Hit("a"), Hit("b"), Hit("c")]);

        Assert.Equal(new[] { "a", "b", "c" }.Take(top), hits.Select(hit => hit.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fuse_NonPositiveTop_Throws(int top)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RankFusion.Fuse(top, [Hit("a")]));
    }

    [Fact]
    public void Fuse_NoListsOrEmptyLists_ReturnsEmpty()
    {
        Assert.Empty(RankFusion.Fuse(3));
        Assert.Empty(RankFusion.Fuse(3, [], []));
    }

    [Fact]
    public void Fuse_DuplicateIdWithinSource_DoesNotAddScoreOrConsumeRank()
    {
        IReadOnlyList<SearchHit> hits = RankFusion.Fuse(3,
            [Hit("a"), Hit("a", 999), Hit("b")], [Hit("c")]);

        Assert.Equal(["a", "c", "b"], hits.Select(hit => hit.Id));
        Assert.Equal(1.0 / 61, hits[0].Score, 12);
        Assert.Equal(1.0 / 62, hits[2].Score, 12);
    }

    [Fact]
    public void Fuse_GraphKeywordAndVector_PreservesEvidenceAndProvenance()
    {
        SearchHit vector = new SearchHit { Id = "doc-2", Title = "Wheel calibration records", Text = "Calibration policy", Score = 0.8, Source = "vector" };
        SearchHit keyword = vector with { Score = 300, Source = "keyword", Evidence = "Matched calibration" };
        SearchHit graph = vector with
        {
            Score = 1,
            Source = "graph",
            Evidence = "MotorService -[DEPENDS_ON]-> SafetyService"
        };

        IReadOnlyList<SearchHit> hits = RankFusion.Fuse(3,
            [vector], [keyword], [graph, Hit("doc-1")]);

        SearchHit fused = hits[0];
        Assert.Equal("doc-2", fused.Id);
        Assert.Equal("Wheel calibration records", fused.Title);
        Assert.Equal("Calibration policy", fused.Text);
        Assert.Equal(3.0 / 61, fused.Score, 12);
        Assert.Equal("vector + keyword + graph", fused.Source);
        Assert.Equal("Matched calibration\nMotorService -[DEPENDS_ON]-> SafetyService", fused.Evidence);
        Assert.Equal("doc-1", hits[1].Id);
    }

    private static SearchHit Hit(string id, double score = 1) => new SearchHit { Id = id, Title = $"Title {id}", Text = $"Text {id}", Score = score, Source = "test" };
}
