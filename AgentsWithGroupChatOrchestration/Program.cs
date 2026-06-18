using AgentsWithGroupChatOrchestration;
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

var navigatorAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
    # PERSONA
    You are the NavigatorAgent that approves or denies proposed move sequences.

    # ACTIONS
    Prefer turning angles of 30°, 45°, or 60° for efficient pathing. 90° turns are allowed but only when geometry requires it.
    Respond APPROVED if the sequence is optimal; otherwise respond DENIED with concrete angle or path improvements.

    # OUTPUT TEMPLATE
    APPROVED or DENIED: <optimality tips>
    """,
    "NavigatorAgent"
  );

var motorsAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
    # PERSONA
    You are the MotorsAgent. Permitted moves: forward, backward, turn left, turn right, stop.

    # ACTIONS
    Break the mission into a move sequence and submit to NavigatorAgent for approval.
    If DENIED, revise based on feedback and resubmit.
    If APPROVED, execute the sequence using MotorTools, then respond with: EXECUTED: <one-line summary>.

    # CONSTRAINTS
    - NEVER execute a DENIED sequence.
    """,
    "MotorsAgent",
    tools: [.. MotorTools.AsAITools()]
  );

var query = """
  # MISSION COMMAND: Exploration Trip

  "There is a tree directly in front of the car. Avoid it and then come back to the original path. The distance to the tree is 50 meters."
  """;

var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(agents =>
  new ApprovedTerminationManager(agents) { MaximumIterationCount = 10 })
  .AddParticipants(motorsAgent, navigatorAgent)
  .Build();

await WorkflowsHelper.PrintToMarkdownAsync(workflow);

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: query);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await WorkflowsHelper.PrintWorkflowExecutionEventsAsync(run);
