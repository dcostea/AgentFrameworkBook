using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentsWithSequentialOrchestration;

public static class AgentResponses
{
  public static async Task<AgentResponse> MissionAbort(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    if (response.Text.Contains("DENIED") == true)
    {
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine("\n*** MISSION ABORT ***");
      Console.ResetColor();
      throw new InvalidOperationException("MISSION ABORT");
    }

    return response;
  }
}