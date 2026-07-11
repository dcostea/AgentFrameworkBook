using AgentWithSequenceOfConcurrentOrchestration;
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
  });

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
  });

var motorsAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
.AsAIAgent(new ChatClientAgentOptions
{
  Name = "MotorsAgent",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      ## PERSONA
      You are the MotorsAgent that executes movement commands.
        
      ## ACTIONS
      If clearance is DENIED, call the Stop tool and then respond with "Mission stopped due to unsafe conditions."
      Otherwise, break the mission into moves (forward, backward, turn left, turn right, stop) and execute them using MotorTools.
        
      ## OUTPUT TEMPLATE
      Respond only with the executed movement sequence.
      """,
    Tools = [.. MotorTools.AsAITools()]
  }
});

// Inner stage: MaintenanceAgent and EnvironmentAgent run concurrently; SafetyAggregator merges their clearances.
Workflow safetyWorkflow = AgentWorkflowBuilder.BuildConcurrent([maintenanceAgent, environmentAgent],
  SafetyAggregator.AggregateClearances);

// Include the aggregated clearance summary (the workflow output) in the SafetyStage response,
// so the next stage receives the merged clearance verdict.
AIAgent safetyStage = safetyWorkflow.AsAIAgent("SafetyStage", includeWorkflowOutputsInResponse: true);

// ⚠️ WARNING: Composing tool-calling orchestrations across boundaries leaks tool messages
// ─────────────────────────────────────────────────────────────────────────────────────────
// When a tool-calling orchestration is composed as an agent (via AsAIAgent()) into an outer
// workflow, its inner agents' tool-call messages leak verbatim into the downstream
// conversation. Every downstream agent at that boundary must strip FunctionCallContent and
// FunctionResultContent from incoming messages before execution; otherwise the provider
// rejects the conversation with HTTP 400. See ToolCallFilteringAgent.
////AIAgent sanitizedMotorsAgent = new ToolCallFilteringAgent(motorsAgent);
////Workflow workflow = AgentWorkflowBuilder.BuildSequential(safetyStage, sanitizedMotorsAgent);

// This is the composition with the motorsAgent directly,
// which will leak tool messages into the downstream conversation and cause HTTP 400 errors.
Workflow workflow = AgentWorkflowBuilder.BuildSequential(safetyStage, motorsAgent);

await WorkflowsHelper.PrintToMarkdownAsync(workflow);

var query = """
  MISSION COMMAND: Exploration Trip
  There is a tree directly in front of the car. Avoid the tree and continue the exploration.
  """;

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
