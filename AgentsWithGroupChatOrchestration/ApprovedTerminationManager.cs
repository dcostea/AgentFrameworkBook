using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AgentsWithGroupChatOrchestration;

/// <summary>
/// A group chat manager that terminates when the MotorsAgent confirms execution after an approval
/// (conversation contains "Approved" and last message contains "Executed"), or when the iteration limit is reached.
/// </summary>
public class ApprovedTerminationManager(IReadOnlyList<AIAgent> agents)
  : RoundRobinGroupChatManager(agents, (chatManager, messages, _) =>
  {
    if (chatManager.IterationCount >= chatManager.MaximumIterationCount)
      return ValueTask.FromResult(true);

    string lastText = messages.LastOrDefault()?.Text ?? string.Empty;
    bool conversationHasApproval = messages.Any(m => 
      m.Text != null &&
      m.Text.Contains(ApprovalState.Approved.ToString(), StringComparison.OrdinalIgnoreCase) &&
      !m.Text.Contains(ApprovalState.Denied.ToString(), StringComparison.OrdinalIgnoreCase));
    bool isExecuted = lastText.Contains(ApprovalState.Executed.ToString(), StringComparison.OrdinalIgnoreCase);

    return ValueTask.FromResult(conversationHasApproval && isExecuted);
  });
