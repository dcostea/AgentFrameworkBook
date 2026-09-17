using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace GroupChatOrchestrationWithCustomManager;

public sealed class InvestigationManager(AIAgent EnvironmentAgent, AIAgent MaintenanceAgent, AIAgent SafetyAgent) 
  : GroupChatManager
{
  private bool _calibrated;
  private int _observationCount;
  private AIAgent? _lastAgent;

  protected override ValueTask<bool> ShouldTerminateAsync(
    IReadOnlyList<ChatMessage> history,
    CancellationToken cancellationToken = default)
  {
    var lastDecision = (history.LastOrDefault()?.Text ?? string.Empty).Split(':', 2)[0].Trim();

    return ValueTask.FromResult(
      IterationCount >= MaximumIterationCount ||
      (_lastAgent == SafetyAgent &&
        (lastDecision.Equals(nameof(SafetyDecision.DENIED), StringComparison.OrdinalIgnoreCase) ||
         (_calibrated && lastDecision.Equals(nameof(SafetyDecision.APPROVED), StringComparison.OrdinalIgnoreCase)))));
  }

  protected override ValueTask<IEnumerable<ChatMessage>> UpdateHistoryAsync(
    IReadOnlyList<ChatMessage> history,
    CancellationToken cancellationToken = default)
  {
    if (_lastAgent == EnvironmentAgent)
      _observationCount++;
    else if (_lastAgent == MaintenanceAgent)
      _calibrated = history.SelectMany(message => message.Contents)
        .OfType<FunctionResultContent>()
        .Any(result => result.Exception is null && result.Result is not null);

    ChatMessage state = new(ChatRole.User, $"""
      InvestigationManager state:
      Completed observations: {_observationCount}
      Calibration completed: {_calibrated}
      """) { AuthorName = nameof(InvestigationManager) };

    return ValueTask.FromResult<IEnumerable<ChatMessage>>([.. history, state]);
  }

  protected override ValueTask<AIAgent> SelectNextAgentAsync(
    IReadOnlyList<ChatMessage> history,
    CancellationToken cancellationToken = default)
  {
    AIAgent nextAgent;
    if (_lastAgent is null)
      nextAgent = EnvironmentAgent;
    else if (_lastAgent == EnvironmentAgent || _lastAgent == MaintenanceAgent)
      nextAgent = SafetyAgent;
    else if (_observationCount < 2)
      nextAgent = EnvironmentAgent;
    else if (!_calibrated)
      nextAgent = MaintenanceAgent;
    else
      nextAgent = EnvironmentAgent;

    _lastAgent = nextAgent;
    return ValueTask.FromResult(nextAgent);
  }
}