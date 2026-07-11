using AgentsWithSequentialOrchestration;
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
  //.AsIChatClient()
  //.AsBuilder()
  //  .Use(ChatClientResponses.MissionAbort, null)
  //.Build()
  .AsAIAgent("""
    ## PERSONA
    You are the SafetyAgent that grants or denies mission clearance.

    ## ACTIONS
    Grant clearance unless the droplet level is Medium or High (rain detected), otherwise deny.

    ## OUTPUT TEMPLATE
    Respond with GRANTED or DENIED and a brief reason.
    """,
    "SafetyAgent")
  .AsBuilder()
    .Use(AgentResponses.MissionAbort, null)
  .Build();

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

var query = """
  # MISSION COMMAND: Exploration Trip

  There is a tree directly in front of the car. Avoid it and then come back to the original path.
  """;

var workflow = AgentWorkflowBuilder.BuildSequential(environmentAgent, safetyAgent, motorsAgent);

await WorkflowsHelper.PrintToMarkdownAsync(workflow);

// Use this for streaming execution to see the events as they happen (observability)
////await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: query);
////await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

////await foreach (WorkflowEvent evt in run.WatchStreamAsync())

await using Run run = await InProcessExecution.RunAsync(workflow, input: query);
foreach (WorkflowEvent evt in run.NewEvents)
{
  switch (evt)
  {
    case ExecutorCompletedEvent completed:
      Console.WriteLine($"[EXECUTOR] {completed.ExecutorId} completed.");
      break;

    case AgentResponseEvent response:
      Console.WriteLine(response.Response.Text);
      break;

    case AgentResponseUpdateEvent update:
      Console.Write(update.Update.Text);
      break;

    case WorkflowOutputEvent output:
      List<Microsoft.Extensions.AI.ChatMessage>? messages = output.As<List<Microsoft.Extensions.AI.ChatMessage>>();
      Console.WriteLine($"\n[WORKFLOW OUTPUT] {messages?.LastOrDefault()?.Text}");
      break;

    case WorkflowErrorEvent error:
      Console.WriteLine($"\n[WORKFLOW ERROR] {error.Exception?.InnerException?.Message ?? error.Exception?.Message ?? "unknown"}");
      break;

    case ExecutorFailedEvent failed:
      Console.Error.WriteLine($"\n[EXECUTOR FAILED] {failed.Data?.Message}");
      break;
  }
}
