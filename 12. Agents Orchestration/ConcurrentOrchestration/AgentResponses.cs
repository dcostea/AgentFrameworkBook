using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentsWithConcurrentOrchestration
{
  public static class AgentResponses
  {
    private static readonly JsonSerializerOptions ResponseJsonSerializerOptions = new()
    {
      Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<AgentResponse> MissionAbort(
      IEnumerable<ChatMessage> messages,
      AgentSession? session,
      AgentRunOptions? options,
      AIAgent innerAgent,
      CancellationToken cancellationToken)
    {
      AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

      try
      {
        Response? parsed = JsonSerializer.Deserialize<Response>(response.Text, ResponseJsonSerializerOptions);
        if (parsed?.Clearance == ClearanceState.DENIED)
        {
          Console.WriteLine("\n*** MISSION ABORT ***");
          throw new InvalidOperationException("MISSION ABORT");
        }
      }
      catch (JsonException)
      {
        throw new InvalidOperationException("CRITICAL FAILURE (invalid JSON)");
      }

      return response;
    }
  }
}
