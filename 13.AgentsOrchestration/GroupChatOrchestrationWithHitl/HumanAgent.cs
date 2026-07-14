using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentsWithGroupChatOrchestration;

/// <summary>
/// A plain <see cref="AIAgent"/> that asks a human via the console to approve or deny the most
/// recently proposed move sequence. Implemented directly against <see cref="AIAgent"/> (instead of
/// wrapping a nested workflow) so its response is a normal <see cref="ChatMessage"/> that flows
/// through the group chat like any other participant's message, with no extra propagation layer
/// that can silently drop or misattribute the output.
/// </summary>
public sealed class HumanAgent : AIAgent
{
  public override string Name { get; } = "HumanAgent";

  protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
    => new(new HumanAgentSession());

  protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
    AgentSession session,
    JsonSerializerOptions? jsonSerializerOptions = null,
    CancellationToken cancellationToken = default)
    => new(default(JsonElement));

  protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
    JsonElement serializedState,
    JsonSerializerOptions? jsonSerializerOptions = null,
    CancellationToken cancellationToken = default)
    => new(new HumanAgentSession());

  protected override Task<AgentResponse> RunCoreAsync(
    IEnumerable<ChatMessage> messages,
    AgentSession? session = null,
    AgentRunOptions? options = null,
    CancellationToken cancellationToken = default)
    => Task.FromResult(new AgentResponse(CreateApprovalDecisionMessage(messages)));

  protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
    IEnumerable<ChatMessage> messages,
    AgentSession? session = null,
    AgentRunOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    ChatMessage responseMessage = CreateApprovalDecisionMessage(messages);
    yield return new AgentResponseUpdate(responseMessage.Role, responseMessage.Contents)
    {
      AuthorName = responseMessage.AuthorName,
      MessageId = responseMessage.MessageId,
    };
    await Task.CompletedTask;
  }

  private static ChatMessage CreateApprovalDecisionMessage(IEnumerable<ChatMessage> messages)
  {
    if (messages.Any(message => message.Text?
      .Contains(nameof(ApprovalState.EXECUTED), StringComparison.Ordinal) == true))
    {
      // The sequence has already been executed and approved; nothing more for the human to decide.
      return new ChatMessage(ChatRole.Assistant, 
        "HUMAN APPROVED: sequence approved already.");
    }

    Console.Write("HUMAN: Approve? [y/n]: ");
    var input = Console.ReadLine();

    if (string.Equals(input, "y", StringComparison.OrdinalIgnoreCase))
    {
      return new ChatMessage(ChatRole.Assistant, 
         $"HUMAN {nameof(ApprovalState.APPROVED)}: the proposed move sequence looks good. Execute it now.");
    }
  
    return new ChatMessage(ChatRole.Assistant, 
      $"HUMAN {nameof(ApprovalState.DENIED)}: please revise the proposed move sequence.");
  }

  private sealed class HumanAgentSession : AgentSession;
}
