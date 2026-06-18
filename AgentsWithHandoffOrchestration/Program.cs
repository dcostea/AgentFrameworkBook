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

ChatClient chatClient = new OpenAIClient(apiKey).GetChatClient(model);

// ---------------------------------------------------------------------------
// Handoff orchestration: a router (EnvironmentAgent) dispatches control to ONE
// specialist based on the sensor readings. Each specialist is a TERMINAL branch
// that fully handles its case.
//
//   EnvironmentAgent --too hot--> FireDetectorAgent (suppress fire) --\
//                    --too wet--> RainDetectorAgent (wipers)         ---+--> MotorsAgent (STOP / run mission)
//                    --safe-----> MotorsAgent (run the mission)
//
//   MotorsAgent is the sole motor authority: detector branches hand off to it
//   for STOP so they need no MotorTools themselves (separation of concerns).
//   The runtime LLM picks ONE branch based on sensor readings — the classic
//   handoff use case that a plain sequence cannot express.
// ---------------------------------------------------------------------------

// Reads sensors and routes to the right specialist. Routing conditions live in its instructions.
var environmentAgent = chatClient.AsAIAgent("""
    ## PERSONA
    You are the EnvironmentAgent. Read the weather conditions using SensorTools, then hand off to exactly one agent.

    ## ROUTING
    Choose the target agent based on the sensor readings:
    - FireDetectorAgent — if the temperature is above 60°C (possible fire).
    - RainDetectorAgent — if the droplet level is High (rain detected).
    - MotorsAgent — if conditions are safe (temperature normal and no heavy rain).
    """,
    "EnvironmentAgent",
    tools: [.. SensorTools.AsAITools()]
  );

// Suppresses a fire (high temperature routed control here), then hands off to MotorsAgent to stop.
var fireDetectorAgent = chatClient.AsAIAgent("""
    ## PERSONA
    You are the FireDetectorAgent. You were invoked because the temperature is dangerously high, call SoundAlarm and StartWaterSprinkle.

    ## HANDOFF
    When finished, respond with a brief message that identifies the danger, then hand off to the MotorsAgent to stop the car.
    """,
    "FireDetectorAgent",
    tools: [.. FireDetectorTools.AsAITools()]
  );

// Handles a too-wet (rain) condition, then hands off to MotorsAgent to stop.
var rainDetectorAgent = chatClient.AsAIAgent("""
    ## PERSONA
    You are the RainDetectorAgent. Handle any rain condition using the available tools.

    ## HANDOFF
    When finished, respond with a brief message that identifies the danger, then hand off to the MotorsAgent to stop the car.
    """,
    "RainDetectorAgent",
    tools: [.. RainDetectorTools.AsAITools()]
  );

// Sole motor authority: stops the car after a hazard, or runs the mission when safe.
var motorsAgent = chatClient.AsAIAgent("""
    ## PERSONA
    You are the MotorsAgent, responsible for car movements using MotorTools.
    The permitted movements are: move forward, turn left, turn right, and stop.

    ## ACTIONS
    Look at the tool results already visible in the conversation:
    - If any of these appear: "Fire alarm sounded", "Water sprinkler started", or "Wipers have started":
      call Stop, then summarize.
    - Otherwise: carry out the mission from the user's command as a short move sequence, then summarize.
    """,
    "MotorsAgent",
    tools: [.. MotorTools.AsAITools()]
  );

var query = """
  # MISSION COMMAND: Exploration Trip

  There is a tree directly in front of the car. Avoid it and then come back to the original path. The distance to the tree is 50 meters.
  """;

var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(environmentAgent)
  // Fan-out: the router dispatches to ONE specialist. Routing conditions live in the
  // EnvironmentAgent's instructions, so these edges only declare the targets.
  .WithHandoffs(environmentAgent, [fireDetectorAgent, rainDetectorAgent, motorsAgent])
  // Fan-in: both detectors converge on the MotorsAgent. Single target, so no reason needed.
  .WithHandoffs([fireDetectorAgent, rainDetectorAgent], motorsAgent)
  .Build();

Console.WriteLine(workflow.ToMermaidString());

await WorkflowsHelper.PrintToMarkdownAsync(workflow);

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: query);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
await WorkflowsHelper.PrintWorkflowExecutionEventsAsync(run);

