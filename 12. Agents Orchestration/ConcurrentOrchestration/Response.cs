using System.Text.Json.Serialization;

namespace AgentsWithConcurrentOrchestration;

public record Response
{
  [JsonPropertyName("clearance")]
  public required ClearanceState Clearance { get; init; }

  [JsonPropertyName("reason")]
  public required string Reason { get; init; }
}
