namespace RagShared;

public static class RankFusion
{
    public static IReadOnlyList<SearchHit> Fuse(int top, params IReadOnlyList<SearchHit>[] rankedLists)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);
        const int k = 60;
        Dictionary<string, SearchHit> fused = new(StringComparer.Ordinal);

        foreach (IReadOnlyList<SearchHit> list in rankedLists)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            int rank = 0;
            foreach (SearchHit hit in list)
            {
                if (!seen.Add(hit.Id))
                {
                    continue;
                }

                // Only rank contributes: keyword, cosine, and graph scores have different scales.
                double contribution = 1.0 / (k + ++rank);
                if (fused.TryGetValue(hit.Id, out SearchHit? previous))
                {
                    fused[hit.Id] = previous with
                    {
                        Score = previous.Score + contribution,
                        Source = string.Join(" + ", previous.Source.Split(" + ").Append(hit.Source).Distinct()),
                        Evidence = string.Join("\n", new[] { previous.Evidence, hit.Evidence }
                            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
                    };
                }
                else
                {
                    fused[hit.Id] = hit with { Score = contribution };
                }
            }
        }

        return fused.Values.OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Id, StringComparer.Ordinal).Take(top).ToArray();
    }
}
