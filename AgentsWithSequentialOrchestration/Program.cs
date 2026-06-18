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
    You are the EnvironmentAgent that reads sensors.

    ## ACTIONS
    Call SensorTools to read temperature, humidity, rain drops, and wind speed.

    ## OUTPUT TEMPLATE
    Respond only with the environmental report, and make the rain status easy to identify.
    """, 
    "EnvironmentAgent",
    tools: [.. SensorTools.AsAITools()]
  );

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
    "SafetyAgent"
  );

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

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: query);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await WorkflowsHelper.PrintWorkflowExecutionEventsAsync(run);
