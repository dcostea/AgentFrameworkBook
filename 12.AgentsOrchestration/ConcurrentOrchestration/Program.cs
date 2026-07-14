using AgentsWithConcurrentOrchestration;
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

ChatResponseFormatJson responseFormat = Microsoft.Extensions.AI.ChatResponseFormat
  .ForJsonSchema<Response>(SafetyAggregator.JsonSerializerOptions);

var maintenanceAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent(new ChatClientAgentOptions
  {
    Name = "MaintenanceAgent",
    ChatOptions = new ChatOptions
    {
      Instructions = """
        ## PERSONA
        You are the MaintenanceAgent that monitors maintenance conditions.

        ## ACTIONS
        Call MaintenanceTools to activate maintenance protocols for dangerous conditions detection.

        ## SAFETY THRESHOLDS
        Grant clearance unless ANY of the following hard limits are exceeded:
        - Battery health below 30%
        - Tire pressure below 28 PSI or above 36 PSI
        - Motor efficiency below 30%

        ## OUTPUT TEMPLATE
        Respond with the maintenance clearance using the requested JSON schema.
        """,
      ResponseFormat = responseFormat,
      Tools = [.. MaintenanceTools.AsAITools()]
    }
  })
  .AsBuilder()
    .Use(AgentResponses.MissionAbort, null)
  .Build();

var environmentAgent = new OpenAIClient(apiKey)
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
        Call SensorTools to read sensors for temperature, humidity, rain drops, and wind speed.

        ## SAFETY THRESHOLDS
        Grant clearance unless ANY of the following hard limits are exceeded:
        - Temperature above 100 Celsius
        - Humidity above 80%
        - Droplet level is Extreme
        - Wind speed above 100 kmph

        ## OUTPUT TEMPLATE
        Respond with the environment clearance using the requested JSON schema.
        """,
      ResponseFormat = responseFormat,
      Tools = [.. SensorTools.AsAITools()]
    }
  })
  .AsBuilder()
    .Use(AgentResponses.MissionAbort, null)
  .Build();

var query = """
  MISSION COMMAND: Exploration Trip
    
  Assess the environment conditions and ensure safety clearance.
  """;

var workflow = AgentWorkflowBuilder.BuildConcurrent("SafetyAssessment", [maintenanceAgent, environmentAgent],
  SafetyAggregator.AggregateClearances);

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
