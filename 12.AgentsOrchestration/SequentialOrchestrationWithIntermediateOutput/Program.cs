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

// Enable agent response output tagging and filtering to allow for intermediate outputs to be captured and displayed.
Futures.EnableAgentResponseOutputTaggingAndFiltering = true;

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

var workflow = AgentWorkflowBuilder.CreateSequentialBuilderWith(environmentAgent, safetyAgent, motorsAgent)
  .WithIntermediateOutputFrom([environmentAgent, safetyAgent])
  .WithOutputFrom(motorsAgent)
  .Build();

await WorkflowsHelper.PrintToMarkdownAsync(workflow);

// Use this for streaming execution to see the events as they happen (observability)
////await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: prompt);
////await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

////await foreach (WorkflowEvent evt in run.WatchStreamAsync())

// Use this for non-streaming execution to see the events after the workflow completes
await using Run run = await InProcessExecution.RunAsync(workflow, input: prompt);
foreach (WorkflowEvent evt in run.NewEvents)
{
  switch (evt)
  {
    case ExecutorCompletedEvent completed:
      Console.WriteLine($"[EXECUTOR] {completed.ExecutorId} completed.");
      break;

    case AgentResponseUpdateEvent update:
      ColorHelper.PrintColored(update.Update.Text, update.IsIntermediate() ? ConsoleColor.Green : ConsoleColor.Yellow);
      break;

    // this event for workflow output is already handled by the AgentResponseUpdateEvent case above when it is 'intermediate', so we can ignore it here
    ////case WorkflowOutputEvent output:
    ////  List<Microsoft.Extensions.AI.ChatMessage>? messages = output.As<List<Microsoft.Extensions.AI.ChatMessage>>();
    ////  ColorHelper.PrintColoredLine($"\n[WORKFLOW OUTPUT] {messages?.LastOrDefault()?.Text}", ConsoleColor.Yellow);
    ////  break;

    case WorkflowErrorEvent error:
      Console.WriteLine($"\n[WORKFLOW ERROR] {error.Exception?.InnerException?.Message ?? error.Exception?.Message ?? "unknown"}");
      break;

    case ExecutorFailedEvent failed:
      Console.Error.WriteLine($"\n[EXECUTOR FAILED] {failed.Data?.Message}");
      break;
  }
}
