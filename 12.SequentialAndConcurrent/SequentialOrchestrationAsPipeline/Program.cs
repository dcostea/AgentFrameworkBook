using AITools;
using Helpers;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

var environmentAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
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
    "EnvironmentAgent",
    tools: [.. SensorTools.AsAITools()]
);

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

var workflow = AgentWorkflowBuilder.BuildSequential("ProcessingPipeline", 
  chainOnlyAgentResponses: true, // each agent receives only the response produced by the previous agent
  environmentAgent, navigatorAgent, motorsAgent);

await WorkflowsHelper.PrintToMarkdownAsync(workflow);

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: prompt);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
  switch (evt)
  {
    case ExecutorCompletedEvent completed:
      ColorHelper.PrintColoredLine($"[EXECUTOR] {completed.ExecutorId} completed.", ConsoleColor.White);
      break;

    case AgentResponseUpdateEvent update:
      ColorHelper.PrintColored(update.Update.Text, ConsoleColor.Green);
      break;

    case WorkflowOutputEvent output:
      List<Microsoft.Extensions.AI.ChatMessage>? messages = output.As<List<Microsoft.Extensions.AI.ChatMessage>>();
      // Print all messages from all agents in the workflow output.
      // When `chainOnlyAgentResponses` is true, the workflow output will contain only the last agent's response,
      // but when it is false, the workflow output will contain all agents' responses.
      var allAgentsMessages = messages?.Where(m => m.Role != ChatRole.Tool && !string.IsNullOrEmpty(m.Text)).Select(m => $"{m.Role}: {m.Text}");
      ColorHelper.PrintColoredLine($"\n[WORKFLOW OUTPUT] {string.Join("\n", allAgentsMessages!)}", ConsoleColor.Yellow);
      break;

    case WorkflowErrorEvent error:
      ColorHelper.PrintColoredLine($"\n[WORKFLOW ERROR] {error.Exception?.InnerException?.Message ?? error.Exception?.Message ?? "unknown"}", ConsoleColor.Red);
      break;

    case ExecutorFailedEvent failed:
      ColorHelper.PrintColoredLine($"\n[EXECUTOR FAILED] {failed.Data?.Message}", ConsoleColor.Red);
      break;
  }
}
