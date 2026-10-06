namespace RagShared;

public sealed record SearchHit
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Text { get; init; }
    public required double Score { get; init; }
    public required string Source { get; init; }
    public string? Evidence { get; init; }
}
