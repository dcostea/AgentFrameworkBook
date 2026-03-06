namespace Adapters;

public record SearchItem
{
  public required string Key { get; init; }
  public required string Text { get; init; }
  public required string SourceName { get; init; }
  public required string[] Keywords { get; init; }
}