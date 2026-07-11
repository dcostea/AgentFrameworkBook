using System.Text.Json.Serialization;

namespace Evaluators.Models;

public class AgentInfo
{
  [JsonPropertyName("name")]
  public required string Name { get; init; }

  [JsonPropertyName("modelProvider")]
  public required string ModelProvider { get; init; }

  [JsonPropertyName("modelName")]
  public required string ModelName { get; init; }

  [JsonPropertyName("instructions")]
  public required string Instructions { get; init; }

  [JsonPropertyName("tools")]
  public required List<string> Tools { get; init; }
}
