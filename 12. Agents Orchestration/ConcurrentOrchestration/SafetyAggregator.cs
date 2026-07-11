using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentsWithConcurrentOrchestration;

public class SafetyAggregator
{
  public static JsonSerializerOptions JsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web)
  {
    Converters = { new JsonStringEnumConverter() }
  };

  public static List<ChatMessage> AggregateClearances(IList<List<ChatMessage>> agentResponses)
  {
    List<Response> responses = ParseResponses(agentResponses);

    string[] deniedReasons = [.. responses
      .Where(response => response.Clearance == ClearanceState.DENIED)
      .Select(response => response.Reason)];

    string summary = deniedReasons.Length > 0
      ? $"{ClearanceState.DENIED}: {string.Join(" ", deniedReasons)}"
      : $"{ClearanceState.GRANTED}: All clearances were granted.";

    return [new ChatMessage(ChatRole.Assistant, summary)];
  }

  // Deserialize all agent responses at the boundary
  private static List<Response> ParseResponses(IList<List<ChatMessage>> agentResponses) =>
    [.. agentResponses
      .SelectMany(messages => messages)
      .Where(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
      .Select(message => JsonSerializer.Deserialize<Response>(message.Text, JsonSerializerOptions)!)];
}
