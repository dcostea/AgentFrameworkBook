using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace AgentsWithConcurrentOrchestration;

public static class ChatClientResponses
{
  public static async Task<ChatResponse> SimulateAgentFailure(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerClient,
    CancellationToken cancellationToken)
  {
    Console.WriteLine("\n*** SIMULATE AGENT FAILURE ***");
    throw new InvalidOperationException("(chat response middleware)");
  }

  public static async IAsyncEnumerable<ChatResponseUpdate> SimulateAgentFailureStreaming(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerClient,
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    Console.WriteLine("\n*** SIMULATE AGENT FAILURE (STREAMING) ***");
    await Task.CompletedTask;
    throw new InvalidOperationException("(streaming chat response middleware)");
#pragma warning disable CS0162 // Unreachable code detected
    yield break;
#pragma warning restore CS0162 // Unreachable code detected
  }
}

public static class ChatClientFunctionCallings
{
  public static async Task<object?> SimulateToolFailure(
    FunctionInvocationContext context, 
    CancellationToken cancellationToken)
  {
    Console.WriteLine("\n*** SIMULATE TOOL FAILURE ***");
    throw new Exception("(function calling middleware)");
  }
}
