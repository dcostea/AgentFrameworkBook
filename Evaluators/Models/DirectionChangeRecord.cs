using System.Text.Json.Serialization;

namespace Evaluators.Models;

/// <summary>
/// JSONL schema for the custom direction-change evaluator records.
/// </summary>
public class DirectionChangeRecord
{
  [JsonPropertyName("id")]
  public required string Id { get; init; }

  [JsonPropertyName("source")]
  public required string Source { get; init; }

  [JsonPropertyName("scenario")]
  public required string Scenario { get; init; }

  [JsonPropertyName("agent")]
  public required AgentInfo Agent { get; init; }

  [JsonPropertyName("userInput")]
  public required string UserInput { get; init; }

  [JsonPropertyName("toolCalls")]
  public required List<ToolCallRecord> ToolCalls { get; init; }

  [JsonPropertyName("finalResponse")]
  public required string FinalResponse { get; init; }

  [JsonPropertyName("expectedBehavior")]
  public required string ExpectedBehavior { get; init; }

  [JsonPropertyName("expectedChanged")]
  public required bool ExpectedChanged { get; init; }

  [JsonPropertyName("expectedFrom")]
  public string? ExpectedFrom { get; init; }

  [JsonPropertyName("expectedTo")]
  public string? ExpectedTo { get; init; }
}
