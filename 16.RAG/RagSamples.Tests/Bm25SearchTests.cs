using RagShared;
using Xunit;

namespace RagSamples.Tests;

public sealed class Bm25SearchTests
{
    [Fact]
    public void Search_UsesHandCalculatedBm25ScoresAndPreservesMetadata()
    {
        Document[] documents =
        [
            new Document { Id = "doc-1", Service = "motors", Title = "calibration", Text = "calibration motor" },
            new Document { Id = "doc-2", Service = "", Title = "", Text = "motor details" },
            new Document { Id = "doc-3", Service = "", Title = "", Text = "robot" }
        ];

        IReadOnlyList<SearchHit> hits = Bm25Search.Search(documents, "calibration motor", 10);

        // N=3, average length=7/3; df(calibration)=1, df(motor)=2.
        // doc-1: ln(8/3)*308/269 + ln(8/5)*154/199; doc-2: ln(8/5)*154/145.
        Assert.Equal(["doc-1", "doc-2"], hits.Select(hit => hit.Id));
        Assert.Equal(1.4867526651982694, hits[0].Score, 12);
        Assert.Equal(0.49917626830236755, hits[1].Score, 12);
        Assert.Equal("calibration", hits[0].Title);
        Assert.Equal("calibration motor", hits[0].Text);
        Assert.All(hits, hit =>
        {
            Assert.Equal("bm25", hit.Source);
            Assert.Null(hit.Evidence);
            Assert.True(double.IsFinite(hit.Score) && hit.Score > 0);
        });
    }

    [Fact]
    public void Search_RareTermsReceiveHigherIdfThanCommonTerms()
    {
        Document[] documents =
        [
            new Document { Id = "doc-1", Service = "", Title = "", Text = "common rare" },
            new Document { Id = "doc-2", Service = "", Title = "", Text = "common other" },
            new Document { Id = "doc-3", Service = "", Title = "", Text = "common other" }
        ];

        SearchHit rare = Assert.Single(Bm25Search.Search(documents, "rare"));
        IReadOnlyList<SearchHit> common = Bm25Search.Search(documents, "common");

        Assert.Equal("doc-1", rare.Id);
        Assert.Equal(0.9808292530117262, rare.Score, 12); // ln(8/3)
        Assert.Equal(3, common.Count);
        Assert.All(common, hit => Assert.Equal(0.13353139262452257, hit.Score, 12)); // ln(8/7)
        Assert.True(rare.Score > common[0].Score);
    }

    [Fact]
    public void Search_TermFrequencyHasDiminishingReturnsAtEqualDocumentLength()
    {
        Document[] documents =
        [
            new Document { Id = "once", Service = "", Title = "", Text = "calibration filler filler" },
            new Document { Id = "twice", Service = "", Title = "", Text = "calibration calibration filler" },
            new Document { Id = "thrice", Service = "", Title = "", Text = "calibration calibration calibration" }
        ];

        IReadOnlyList<SearchHit> hits = Bm25Search.Search(documents, "calibration");

        // Equal lengths: ln(8/7) times 11/7, 11/8, and 1, respectively.
        Assert.Equal(["thrice", "twice", "once"], hits.Select(hit => hit.Id));
        Assert.Equal(0.20983504555282118, hits[0].Score, 12);
        Assert.Equal(0.18360566485871854, hits[1].Score, 12);
        Assert.Equal(0.13353139262452257, hits[2].Score, 12);
        Assert.True(hits[0].Score - hits[1].Score < hits[1].Score - hits[2].Score);
    }

    [Fact]
    public void Search_ShorterDocumentsRankHigherForEqualTermFrequency()
    {
        Document[] documents =
        [
            new Document { Id = "long", Service = "", Title = "", Text = "calibration detail detail" },
            new Document { Id = "short", Service = "", Title = "", Text = "calibration" }
        ];

        IReadOnlyList<SearchHit> hits = Bm25Search.Search(documents, "calibration");

        // Average length=2: ln(6/5) times 44/35 and 44/53.
        Assert.Equal(["short", "long"], hits.Select(hit => hit.Id));
        Assert.Equal(0.22920424282668578, hits[0].Score, 12);
        Assert.Equal(0.15136129243271704, hits[1].Score, 12);
    }

    [Fact]
    public void Search_NonmatchingDocumentsStillContributeToCorpusStatistics()
    {
        Document match = new Document { Id = "match", Service = "", Title = "", Text = "calibration" };
        Document nonmatch = new Document { Id = "other", Service = "", Title = "", Text = "robot detail detail" };

        SearchHit alone = Assert.Single(Bm25Search.Search([match], "calibration"));
        SearchHit withNonmatch = Assert.Single(Bm25Search.Search([match, nonmatch], "calibration"));

        Assert.Equal("match", withNonmatch.Id);
        Assert.Equal(0.28768207245178085, alone.Score, 12); // ln(4/3)
        Assert.Equal(0.8713850269896456, withNonmatch.Score, 12); // ln(2)*44/35
    }

    [Fact]
    public void Search_EmptyDocumentsStillContributeToCorpusSizeAndAverageLength()
    {
        Document[] documents = [new Document { Id = "match", Service = "", Title = "", Text = "calibration" }, new Document { Id = "empty", Service = "", Title = "", Text = "" }];

        SearchHit hit = Assert.Single(Bm25Search.Search(documents, "calibration"));

        Assert.Equal("match", hit.Id);
        Assert.Equal(0.49191090233286444, hit.Score, 12); // N=2, average length=1/2: ln(2)*22/31
    }

    [Theory]
    [InlineData("motors")]
    [InlineData("policy")]
    [InlineData("days")]
    public void Search_TokenizesServiceTitleAndText(string query)
    {
        Document[] documents = [new Document { Id = "doc-1", Service = "Motors", Title = "Calibration policy", Text = "seven days" }];

        SearchHit hit = Assert.Single(Bm25Search.Search(documents, query));

        Assert.Equal("doc-1", hit.Id);
        Assert.Equal(0.28768207245178085, hit.Score, 12);
        Assert.Equal("Calibration policy", hit.Title);
        Assert.Equal("seven days", hit.Text);
    }

    [Fact]
    public void Search_IgnoresCaseAndPunctuationAndCountsEachQueryTokenOnce()
    {
        Document[] documents =
        [
            new Document { Id = "match", Service = "", Title = "", Text = "Calibration café 42 A7" },
            new Document { Id = "other", Service = "", Title = "", Text = "calibrations cafe 43 A" }
        ];

        SearchHit baseline = Assert.Single(Bm25Search.Search(documents, "calibration café 42 a7"));
        SearchHit repeated = Assert.Single(Bm25Search.Search(documents, "CALIBRATION, Café!42/A7; calibration CALIBRATION café 42"));

        Assert.Equal("match", repeated.Id);
        Assert.Equal(2.772588722239781, baseline.Score, 12); // Four distinct terms, each ln(2).
        Assert.Equal(baseline, repeated);
    }

    [Fact]
    public void Search_UsesBagOfWordsAndTreatsOrAsAnOrdinaryToken()
    {
        Document[] documents =
        [
            new Document { Id = "a", Service = "", Title = "", Text = "calibration" },
            new Document { Id = "b", Service = "", Title = "", Text = "suspicious" },
            new Document { Id = "c", Service = "", Title = "", Text = "or" }
        ];

        IReadOnlyList<SearchHit> partialMatches = Bm25Search.Search(documents, "calibration suspicious");
        IReadOnlyList<SearchHit> includingOr = Bm25Search.Search(documents, "calibration OR suspicious");

        Assert.Equal(["a", "b"], partialMatches.Select(hit => hit.Id));
        Assert.Equal(["a", "b", "c"], includingOr.Select(hit => hit.Id));
        Assert.All(includingOr, hit => Assert.Equal(0.9808292530117262, hit.Score, 12));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public void Search_ScoresAllDocumentsBeforeTopAndBreaksTiesByOrdinalId(int top)
    {
        Document[] documents =
        [
            new Document { Id = "z", Service = "", Title = "", Text = "calibration detail detail" },
            new Document { Id = "a", Service = "", Title = "", Text = "calibration" },
            new Document { Id = "B", Service = "", Title = "", Text = "calibration" }
        ];

        IReadOnlyList<SearchHit> hits = Bm25Search.Search(documents, "calibration", top);

        Assert.Equal(new[] { "B", "a", "z" }.Take(top), hits.Select(hit => hit.Id));
        Assert.Equal(Bm25Search.Search(documents.Reverse().ToArray(), "calibration", top), hits);
    }

    [Fact]
    public void Search_DefaultTopReturnsThreeHits()
    {
        Document[] documents =
        [
            new Document { Id = "d", Service = "", Title = "", Text = "calibration" }, new Document { Id = "c", Service = "", Title = "", Text = "calibration" },
            new Document { Id = "b", Service = "", Title = "", Text = "calibration" }, new Document { Id = "a", Service = "", Title = "", Text = "calibration" }
        ];

        Assert.Equal(["a", "b", "c"], Bm25Search.Search(documents, "calibration").Select(hit => hit.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("!!! ---")]
    [InlineData("zzzxqvnonexistent93")]
    [InlineData("calibrations")]
    [InlineData("doc-1")]
    public void Search_BlankOrUnknownTokensReturnNoMatchesWithoutStemming(string query)
    {
        Document[] documents = [new Document { Id = "doc-1", Service = "", Title = "", Text = "calibration" }];

        Assert.Empty(Bm25Search.Search(documents, query));
    }

    [Fact]
    public void Search_EmptyCorpusOrAllEmptyTextReturnsNoMatches()
    {
        Assert.Empty(Bm25Search.Search([], "calibration"));
        Assert.Empty(Bm25Search.Search([new Document { Id = "a", Service = "", Title = "", Text = "" }, new Document { Id = "b", Service = " ", Title = "!!!", Text = "\t" }], "calibration"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Search_NonpositiveTopThrowsEvenForEmptyInput(int top)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Bm25Search.Search([], "", top));

        Assert.Equal("top", exception.ParamName);
    }
}
