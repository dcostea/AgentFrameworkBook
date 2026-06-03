using System.Text.Json.Serialization;

namespace Evaluators.Models;

/// <summary>
/// JSONL schema for standard evaluator sample records.
/// The source field is metadata that identifies where a sample came from; tests do not assert on it.
/// </summary>
public class EvaluationRecord
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

  [JsonPropertyName("groundTruth")]
  public string? GroundTruth { get; init; }

  [JsonPropertyName("retrievedContextChunks")]
  public List<string>? RetrievedContextChunks { get; init; }

  [JsonPropertyName("shouldPass")]
  public required bool ShouldPass { get; init; }
}
