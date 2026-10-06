using System.Text.Json;

namespace RagShared;

public sealed record Document
{
    public required string Id { get; init; }
    public required string Service { get; init; }
    public required string Title { get; init; }
    public required string Text { get; init; }
}

public sealed record Dependency(string From, string To);

public static class SampleData
{
    public static IReadOnlyList<Document> Documents { get; }
    public static IReadOnlyList<Dependency> Dependencies { get; }

    static SampleData()
    {
        using Stream stream = typeof(SampleData).Assembly.GetManifestResourceStream("RagShared.corpus.json")
            ?? throw new InvalidOperationException("The embedded teaching corpus is missing.");
        Corpus corpus = JsonSerializer.Deserialize<Corpus>(stream)
            ?? throw new InvalidOperationException("The embedded teaching corpus is empty.");
        Documents = corpus.Documents;
        Dependencies = corpus.Dependencies;
    }

    private sealed record Corpus(Document[] Documents, Dependency[] Dependencies);
}
