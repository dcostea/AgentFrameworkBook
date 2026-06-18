using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentsWithConcurrentOrchestration;

public class Aggregators
{
  public static JsonSerializerOptions JsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web)
  {
    Converters = { new JsonStringEnumConverter() }
  };

  public static Response AggregateClearances(IList<List<ChatMessage>> aggregateClearances)
  {
    // Deserialize all agent responses at the boundary
    List<Response> responses = [.. aggregateClearances
      .SelectMany(messages => messages)
      .Where(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
      .Select(message => JsonSerializer.Deserialize<Response>(message.Text, JsonSerializerOptions)!)];

    // Collect reasons from denied responses and aggregate
    string[] deniedReasons = [.. responses
      .Where(r => r.Clearance == Clearance.DENIED)
      .Select(r => r.Reason)];

    return deniedReasons.Length > 0
      ? new Response { Clearance = Clearance.DENIED, Reason = string.Join(" ", deniedReasons) }
      : new Response { Clearance = Clearance.GRANTED, Reason = "All clearances were granted." };
  }

  public static List<ChatMessage> ToMessages(Response response) =>
    [new ChatMessage(ChatRole.Assistant, $"{response.Clearance}: {response.Reason}")];
}
