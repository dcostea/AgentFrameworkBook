using System.Text.RegularExpressions;

namespace RagShared;

public static class Bm25Search
{
    public static IReadOnlyList<SearchHit> Search(IReadOnlyList<Document> documents, string query, int top = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);
        if (documents.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        string[] queryTerms = Tokenize(query).Distinct(StringComparer.Ordinal).ToArray();
        if (queryTerms.Length == 0)
        {
            return [];
        }

        // Scan the complete tiny teaching corpus: even nonmatches contribute to N, df and average length.
        string[][] tokens = documents.Select(document =>
            Tokenize($"{document.Service} {document.Title} {document.Text}")).ToArray();
        double averageLength = tokens.Average(words => words.Length);
        if (averageLength == 0)
        {
            return [];
        }

        Dictionary<string, int> documentFrequencies = queryTerms.ToDictionary(
            term => term, term => tokens.Count(words => words.Contains(term, StringComparer.Ordinal)),
            StringComparer.Ordinal);
        const double k1 = 1.2;
        const double b = 0.75;
        List<SearchHit> hits = [];
        for (int i = 0; i < documents.Count; i++)
        {
            double score = 0;
            foreach (string term in queryTerms)
            {
                int frequency = tokens[i].Count(word => word == term);
                if (frequency == 0)
                {
                    continue;
                }

                int documentFrequency = documentFrequencies[term];
                double idf = Math.Log(1 + (documents.Count - documentFrequency + 0.5) / (documentFrequency + 0.5));
                score += idf * frequency * (k1 + 1) /
                    (frequency + k1 * (1 - b + b * tokens[i].Length / averageLength));
            }

            if (score > 0)
            {
                Document document = documents[i];
                hits.Add(new SearchHit { Id = document.Id, Title = document.Title, Text = document.Text, Score = score, Source = "bm25" });
            }
        }

        return hits.OrderByDescending(hit => hit.Score).ThenBy(hit => hit.Id, StringComparer.Ordinal)
            .Take(top).ToArray();
    }

    private static string[] Tokenize(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(match => match.Value).ToArray();
}
