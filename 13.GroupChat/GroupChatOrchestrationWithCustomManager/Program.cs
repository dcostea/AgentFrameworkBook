using AITools;
using GroupChatOrchestrationWithCustomManager;
using Helpers;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

var chatClient = new OpenAIClient(apiKey).GetChatClient(model);

var environmentAgent = chatClient.AsAIAgent("""
  # PERSONA
  You are EnvironmentAgent. You collect Robby's environmental readings.

  # ACTIONS
  On every turn, read the temperature and humidity using each tool exactly once.
  The two measurements together form one observation, not two observations.
  Report only this turn's observation. Do not repeat earlier readings or assess safety.

  # OUTPUT TEMPLATE
  Observation: temperature = <value> Celsius; humidity = <value>%.
  """,
  "EnvironmentAgent",
  tools: [.. SensorTools.AsAITools()]);

var maintenanceAgent = chatClient.AsAIAgent("""
  # PERSONA
  You are MaintenanceAgent. You maintain Robby's sensors.

  # ACTIONS
  Call your calibration tool and wait for its result.
  Confirm that calibration is complete only if the tool reports success.
  If the tool fails, report the failure instead of claiming calibration is complete.
  Do not include or repeat any sensor readings.
  """,
  "MaintenanceAgent",
  tools: [AIFunctionFactory.Create(MaintenanceTools.CalibrateSensorsAsync)]);

var safetyAgent = chatClient.AsAIAgent("""
  # PERSONA
  You are SafetyAgent. You decide whether Robby's readings are safe.

  # EVIDENCE
  One observation is one EnvironmentAgent report containing a temperature/humidity pair.
  Tool results and repeated mentions of the same readings are not additional observations.
  Use the latest InvestigationManager state for the completed observation count and calibration status.
  Your previous decisions are not measurements or evidence that a tool ran.
  CALIBRATION always requests more work; it never confirms that calibration happened.

  # ACTIONS
  Apply these rules in order, using the actual readings from all completed observations:
  1. If any temperature is greater than 90 Celsius or any humidity is greater than 95%, respond with DENIED.
  2. If there are at least two observations and any temperature is greater than 60 Celsius
     or any humidity is greater than 80%, respond with DENIED. Check both observations, not just the latest.
     Apply these lower limits only when the completed observation count is at least two.
  3. If there is only one observation, respond with CALIBRATION and request a second observation.
  4. If calibration is not complete, respond with CALIBRATION and request sensor calibration.
  5. Otherwise, respond with APPROVED only when the manager's state confirms calibration is complete
     and MaintenanceAgent has actually reported successful calibration.

  Values equal to a limit do not exceed it. Verify each numeric comparison before choosing a decision.
  For DENIED, cite an actual offending measurement and its limit. Never invent readings or calibration evidence.

  # OUTPUT TEMPLATE
  Return exactly one line using one of these formats:
  APPROVED: <reason>
  DENIED: <reason>
  CALIBRATION: <reason>
  """,
  "SafetyAgent");

var agents = new[] { environmentAgent, maintenanceAgent, safetyAgent };
var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(_ =>
    new InvestigationManager(environmentAgent, maintenanceAgent, safetyAgent) { MaximumIterationCount = 10 })
  .AddParticipants(agents)
  .WithOutputFrom(safetyAgent)
  .WithName("SensorInvestigation")
  .Build();

await WorkflowsHelper.PrintToMarkdownAsync(workflow);

const string prompt = "Check Robby's environment and decide whether it is safe.";
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: prompt);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
  switch (evt)
  {
    case AgentResponseUpdateEvent update:
      ColorHelper.PrintColored(update.Update.Text, ConsoleColor.Green);
      break;

    case WorkflowOutputEvent output:
      string? result = output
        .As<List<ChatMessage>>()?
        .LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Text))?.Text;
      ColorHelper.PrintColoredLine($"\n[WORKFLOW OUTPUT] {result}", ConsoleColor.Yellow);
      break;

    case WorkflowErrorEvent error:
      ColorHelper.PrintColoredLine($"\n[WORKFLOW ERROR] {error.Exception?.InnerException?.Message ?? error.Exception?.Message ?? "unknown"}", ConsoleColor.Red);
      break;

    case ExecutorFailedEvent failed:
      ColorHelper.PrintColoredLine($"\n[EXECUTOR FAILED] {failed.Data?.Message}", ConsoleColor.Red);
      break;
  }
}