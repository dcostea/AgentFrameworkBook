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
      You are the EnvironmentAgent that starts a mission by reading sensors and interpreting the mission command.

      ## ACTIONS
      Call SensorTools to read temperature, humidity, rain drops, and wind speed.
      Extract the destination, obstacles, and required outcome from the mission command.
      Include all mission details in your report, including the original command, because the next agent receives only your response.

      ## OUTPUT TEMPLATE
      Respond only with a self-contained report containing:
      - Mission objective
      - Known obstacles
      - Temperature
      - Humidity
      - Rain droplet level
      - Wind speed
      """,
    Tools = [.. SensorTools.AsAITools()],
  }
});

var navigatorAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
    ## PERSONA
    You are the NavigatorAgent that transforms an environment report into a route plan.

    ## ACTIONS
    Use only the received environment report.
    Plan a safe route that completes the mission objective and avoids every known obstacle.
    Express the route as an ordered list using only: forward, backward, turn left, turn right, and stop.

    ## OUTPUT TEMPLATE
    Respond only with the ordered movement plan, with one movement per line.
    """,
    "NavigatorAgent");

var motorsAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
    ## PERSONA
    You are the MotorsAgent that executes an ordered movement plan.

    ## ACTIONS
    Execute each received movement in order using MotorTools.
    Do not reinterpret the mission or alter the route plan.

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

// In a processing pipeline, each agent receives only the response produced by the previous agent.
var workflow = AgentWorkflowBuilder.BuildSequential("ProcessingPipeline", chainOnlyAgentResponses: true, 
  environmentAgent, navigatorAgent, motorsAgent);

await WorkflowsHelper.PrintToMarkdownAsync(workflow);

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: prompt);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
  switch (evt)
  {
    case ExecutorCompletedEvent completed:
      Console.WriteLine($"[EXECUTOR] {completed.ExecutorId} completed.");
      break;

    case AgentResponseUpdateEvent update:
      ColorHelper.PrintColored(update.Update.Text, ConsoleColor.Green);
      break;

    case WorkflowOutputEvent output:
      List<Microsoft.Extensions.AI.ChatMessage>? messages = output.As<List<Microsoft.Extensions.AI.ChatMessage>>();
      ColorHelper.PrintColoredLine($"\n[WORKFLOW OUTPUT] {messages?.LastOrDefault()?.Text}", ConsoleColor.Yellow);
      break;

    case WorkflowErrorEvent error:
      Console.WriteLine($"\n[WORKFLOW ERROR] {error.Exception?.InnerException?.Message ?? error.Exception?.Message ?? "unknown"}");
      break;

    case ExecutorFailedEvent failed:
      Console.Error.WriteLine($"\n[EXECUTOR FAILED] {failed.Data?.Message}");
      break;
  }
}
