using Microsoft.Agents.AI.Workflows;

Console.Write("Enter weather (type 'clear', 'rain', etc.): ");
string weather = Console.ReadLine() ?? "clear";

SafetyCheckExecutor safety = new();
MotorsPlanExecutor motors = new();

WorkflowBuilder builder = new(safety);
builder.AddEdge(safety, motors).WithOutputFrom(motors);
Workflow workflow = builder.Build();

string mission = $"""
  ## Weather Report: {weather}.

  ## Mission Command:
  There is a tree ahead. Avoid it and return to original path.
  """;

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: mission);

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
  if (evt is ExecutorCompletedEvent completed)
  {
    Console.WriteLine($"{completed.ExecutorId}: {completed.Data}");
  }
  else if (evt is ExecutorFailedEvent failed)
  {
    Console.Error.WriteLine($"Executor '{failed.ExecutorId}' failed: {failed.Data}");
  }
  else if (evt is WorkflowErrorEvent error)
  {
    Console.Error.WriteLine(error.Exception?.ToString() ?? "Unknown workflow error.");
  }
}

internal sealed record SafetyDecision(bool IsSafe, string Mission);

internal sealed class SafetyCheckExecutor() : Executor<string, SafetyDecision>("SafetyCheckExecutor")
{
  public override ValueTask<SafetyDecision> HandleAsync(
      string message,
      IWorkflowContext context,
      CancellationToken cancellationToken = default)
  {
    string firstLine = message.Split('\n')[0];
    bool dangerous = firstLine.Contains("rain", StringComparison.OrdinalIgnoreCase);

    SafetyDecision decision = dangerous
      ? new SafetyDecision(false, message)
      : new SafetyDecision(true, message);

    return ValueTask.FromResult(decision);
  }
}

internal sealed class MotorsPlanExecutor() : Executor<SafetyDecision, string>("MotorsPlanExecutor")
{
  public override ValueTask<string> HandleAsync(
      SafetyDecision input,
      IWorkflowContext context,
      CancellationToken cancellationToken = default)
  {
    if (!input.IsSafe)
    {
      return ValueTask.FromResult("stop");
    }

    // Simple deterministic movement plan for the mission
    string movements = "forward, turn_right, forward, turn_left, forward, turn_left, forward, turn_right, forward";
    return ValueTask.FromResult(movements);
  }
}
