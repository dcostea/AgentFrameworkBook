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
  .ForJsonSchema<Response>(Aggregators.JsonSerializerOptions);

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
        Respond with the environmental clearance using the requested JSON schema.
        """,
      ResponseFormat = responseFormat,
      Tools = [.. SensorTools.AsAITools()]
    }
  });

var query = """
  MISSION COMMAND: Exploration Trip
    
  Assess the environmental conditions and ensure safety clearance.
  """;

var workflow = AgentWorkflowBuilder.BuildConcurrent([maintenanceAgent, environmentAgent], 
  results => Aggregators.ToMessages(Aggregators.AggregateClearances(results)));

await WorkflowsHelper.PrintToMarkdownAsync(workflow);

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: query);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await WorkflowsHelper.PrintWorkflowExecutionEventsAsync(run);
