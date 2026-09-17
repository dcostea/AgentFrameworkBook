using AITools;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

ChatClientAgent environmentAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent(new ChatClientAgentOptions
{
  Name = "EnvironmentAgent",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      ## PERSONA
      You are the EnvironmentAgent that reads sensors.

      ## ACTIONS
      Call SensorTools to read temperature, humidity, rain drops, and wind speed.

      ## OUTPUT TEMPLATE
      Respond only with the environment report, and make the rain status easy to identify.
      """,
    Tools = [.. SensorTools.AsAITools()],
  }
});

var safetyAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
    ## PERSONA
    You are the SafetyAgent that grants or denies mission clearance.

    ## ACTIONS
    Grant clearance unless the droplet level is Medium or High (rain detected), otherwise deny.

    ## OUTPUT TEMPLATE
    Respond with GRANTED or DENIED and a brief reason.
    """,
    "SafetyAgent");

var motorsAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
    ## PERSONA
    You are the MotorsAgent that executes movement commands.

    ## ACTIONS
    If clearance is DENIED, call the Stop tool and then respond with "Mission stopped due to unsafe conditions."
    Otherwise, break the mission into moves (forward, backward, turn left, turn right, stop) and execute them using MotorTools.

    ## OUTPUT TEMPLATE
    Respond only with the executed movement sequence.
    """,
    "MotorsAgent",
    tools: [.. MotorTools.AsAITools()]
  );

var prompt = """
  # MISSION COMMAND: Exploration Trip

  There is a tree directly in front of the car. Avoid it and then come back to the original path.
  """;

CheckpointManager checkpointManager = CheckpointManager.Default;

var workflow = AgentWorkflowBuilder.BuildSequential("SafeExecutionWithCheckpoints", environmentAgent, safetyAgent, motorsAgent);
await WorkflowsHelper.PrintToMarkdownAsync(workflow);

// Use this for streaming execution to see the events as they happen (observability)
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: prompt, checkpointManager: checkpointManager);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

CheckpointInfo? checkpoint = await ProcessWorkflowAsync(run);

async Task<CheckpointInfo?> ProcessWorkflowAsync(StreamingRun run, bool captureCheckpoint = true)
{
  CheckpointInfo? checkpoint = null;
  await foreach (WorkflowEvent evt in run.WatchStreamAsync())
  {
    switch (evt)
    {
      case SuperStepCompletedEvent superStep:
        ColorHelper.PrintColoredLine($"[SUPER STEP] {superStep.StepNumber} completed.", ConsoleColor.White);
        if (captureCheckpoint && checkpoint is null)
        {
          if (superStep.CompletionInfo!.ActivatedExecutors.Any(id => id.StartsWith("MotorsAgent", StringComparison.Ordinal)))
          {
            checkpoint = superStep.CompletionInfo?.Checkpoint
              ?? throw new InvalidOperationException("Checkpoint is null.");
            ColorHelper.PrintColoredLine($"*** CHECKPOINT CREATED ***", ConsoleColor.Yellow);
          }
        }
        break;

      case ExecutorCompletedEvent completed:
        ColorHelper.PrintColoredLine($"[EXECUTOR] {completed.ExecutorId} completed.", ConsoleColor.White);
        break;

      case AgentResponseUpdateEvent update:
        ColorHelper.PrintColored(update.Update.Text, ConsoleColor.Green);
        break;

      case WorkflowOutputEvent output:
        List<Microsoft.Extensions.AI.ChatMessage>? messages = output.As<List<Microsoft.Extensions.AI.ChatMessage>>();
        ColorHelper.PrintColoredLine($"\n[WORKFLOW OUTPUT] {messages?.LastOrDefault()?.Text}", ConsoleColor.Yellow);
        break;

      case WorkflowErrorEvent error:
        ColorHelper.PrintColoredLine($"\n[WORKFLOW ERROR] {error.Exception?.InnerException?.Message ?? error.Exception?.Message ?? "unknown"}", ConsoleColor.Red);
        break;

      case ExecutorFailedEvent failed:
        ColorHelper.PrintColoredLine($"\n[EXECUTOR FAILED] {failed.Data?.Message}", ConsoleColor.Red);
        break;
    }
  }

  return checkpoint;
}

if (checkpoint is not null) 
{
  Console.ForegroundColor = ConsoleColor.Yellow;
  Console.WriteLine("*** RESUMING FROM CHECKPOINT ***");
  Console.ResetColor();

  // Resume from the saved checkpoint on a fresh run instance instead of restoring
  // state in-place on the run that just finished (which still has queued, unpublished
  // state updates from its final superstep).
  Workflow resumedWorkflow = AgentWorkflowBuilder.BuildSequential("SafeExecutionFromCheckpoint", environmentAgent, safetyAgent, motorsAgent);
  await using StreamingRun resumedRun = await InProcessExecution.ResumeStreamingAsync(resumedWorkflow, checkpoint, checkpointManager, CancellationToken.None);
  _ = await ProcessWorkflowAsync(resumedRun, captureCheckpoint: false);
}
