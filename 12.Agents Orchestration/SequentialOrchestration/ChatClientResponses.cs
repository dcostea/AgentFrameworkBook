using Microsoft.Extensions.AI;

namespace AgentsWithSequentialOrchestration;

public static class ChatClientResponses
{
  public static async Task<ChatResponse> MissionAbort(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerClient,
    CancellationToken cancellationToken)
  {
    ChatResponse response = await innerClient.GetResponseAsync(messages, options, cancellationToken);

    if (response.Text.Contains("DENIED") == true)
    {
      Console.WriteLine("\n*** MISSION ABORT ***");
      throw new InvalidOperationException("MISSION ABORT");
    }

    return response;
  }
}
