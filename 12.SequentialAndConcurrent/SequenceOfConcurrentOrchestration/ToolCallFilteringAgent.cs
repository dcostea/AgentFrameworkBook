using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentWithSequenceOfConcurrentOrchestration;

// Strips FunctionCallContent / FunctionResultContent messages produced by upstream agents
// so the wrapped agent receives a conversation that is valid for the chat completion API.
// Required at every composition boundary where a tool-calling orchestration (concurrent,
// group chat, …) feeds into a downstream agent via AsAIAgent() + BuildSequential().
public sealed class ToolCallFilteringAgent(AIAgent innerAgent) : DelegatingAIAgent(innerAgent)
{
  protected override Task<AgentResponse> RunCoreAsync(
    IEnumerable<ChatMessage> messages,
    AgentSession? session = null,
    AgentRunOptions? options = null,
    CancellationToken cancellationToken = default) =>
    base.RunCoreAsync(WithoutToolCallMessages(messages), session, options, cancellationToken);

  protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
    IEnumerable<ChatMessage> messages,
    AgentSession? session = null,
    AgentRunOptions? options = null,
    CancellationToken cancellationToken = default) =>
    base.RunCoreStreamingAsync(WithoutToolCallMessages(messages), session, options, cancellationToken);

  private static List<ChatMessage> WithoutToolCallMessages(IEnumerable<ChatMessage> messages) =>
    [.. messages.Where(m => !m.Contents.Any(c => c is FunctionCallContent or FunctionResultContent))];
}
