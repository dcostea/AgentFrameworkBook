using System.Text.Json.Serialization;

namespace Evaluators.Models;

public class ToolCallRecord
{
  [JsonPropertyName("name")]
  public required string Name { get; init; }

  [JsonPropertyName("arguments")]
  public required Dictionary<string, object?> Arguments { get; init; }

  [JsonPropertyName("result")]
  public required string Result { get; init; }
}
