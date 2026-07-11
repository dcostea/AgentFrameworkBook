using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AgentsWithGroupChatOrchestration;

/// <summary>
/// A group chat manager that terminates when the MotorsAgent confirms execution after an approval
/// (conversation contains "APPROVED" and last message contains "EXECUTED"), or when the iteration limit is reached.
/// </summary>
public class ApprovedTerminationManager(IReadOnlyList<AIAgent> agents)
  : RoundRobinGroupChatManager(agents, (chatManager, messages, _) =>
  {
    if (chatManager.IterationCount >= chatManager.MaximumIterationCount)
      return ValueTask.FromResult(true);

    return ValueTask.FromResult(
      (messages.LastOrDefault()?.Text ?? string.Empty).Contains(nameof(ApprovalState.EXECUTED)) &&
      messages.Any(m => m.Text != null
        && m.Text.Contains(nameof(ApprovalState.APPROVED))
        && !m.Text.Contains(nameof(ApprovalState.DENIED))));
  });
