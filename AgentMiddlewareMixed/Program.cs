// =============================================================================
// Agent Middleware Mixed: SharedFunction + Response + FunctionCalling
// =============================================================================
//
// PURPOSE: Demonstrate the three Agent middleware types composed into one agent.
//
// Pipeline order (outer → inner):
//   SharedFunction   — pre/post run message and session processing
//   Response         — full AgentResponse inspection and mutation
//   FunctionCalling  — per-tool invocation blocking and auditing
// =============================================================================

using AITools;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Middleware;
using OpenAI;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
string? model = configuration["OpenAI:ModelId"];
string? apiKey = configuration["OpenAI:ApiKey"];

Console.OutputEncoding = System.Text.Encoding.UTF8;

ColorHelper.PrintColoredLine("""
  Agent Middleware Mixed — Full Pipeline

  SharedFunction:
    1. AgentGuardrails       — OperatorName gate + MissionTag prefix from StateBag
    2. PersistentRemoveEmail — persistent GDPR sanitization

  Response:
    3. MovementSequenceAuditor — warning footer for illegal completed sequences
    4. CaptainsLog             — journal prefix with agent name and tool call count

  FunctionCalling:
    5. AuditAgentFunctionCalls — wraps every tool call with timing
    6. PreventDangerousMoves   — blocks forward↔backward reversals before tools run
  """, ConsoleColor.DarkGray);

// =============================================================================
// Create base agent
// =============================================================================

ChatClientAgent motorsAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent(new ChatClientAgentOptions
  {
    Name = "MotorsAgent",
    Description = "Controls robot car movements: forward, backward, turn left/right, stop.",
    ChatOptions = new ChatOptions
    {
      Instructions = """
        You are the MotorsAgent controlling a robot car.
        Break down complex movement commands into basic moves: forward, backward, turn_left, turn_right, stop.
        Respond with the sequence of moves needed to accomplish the task.
        """,
      Tools = [.. MotorTools.AsAITools()],
    }
  });

// =============================================================================
// Compose the full pipeline
//
// Order: SharedFunction outermost, FunctionCalling innermost.
// SharedFunction registered first (runs first on the way in, last on the way out).
// Response registered after SharedFunction.
// FunctionCalling registered last (innermost — closest to tool execution).
// =============================================================================

AIAgent motorsAgentWithFullPipeline = motorsAgent
  .AsBuilder()
  // SharedFunction — Prepare layer (outermost)
  .Use(sharedFunc: AgentSharedFunctions.AgentGuardrails)
  .Use(sharedFunc: AgentSharedFunctions.PersistentRemoveEmail)
  // Response — Handle layer
  .Use(runFunc: AgentResponses.MovementSequenceAuditor, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.CaptainsLog, runStreamingFunc: null)
  // FunctionCalling — Invoke layer (innermost)
  .Use(AgentFunctionCallings.PreventDangerousMoves)
  .Use(AgentFunctionCallings.AuditAgentFunctionCalls)
  .Build();

// =============================================================================
// TEST 1: Full pipeline — guardrails, sanitization, journal, tool audit
// =============================================================================

ColorHelper.PrintColoredLine("""
  ===== TEST 1: Full Pipeline (authorized operator + email sanitization) =====
  OperatorName='driver' passes the SharedFunction gate.
  MissionTag is prefixed to the latest user message.
  Email is redacted before the LLM sees it.
  """);

AgentSession session1 = await motorsAgentWithFullPipeline.CreateSessionAsync();
session1.StateBag.SetValue("OperatorName", "driver");
session1.StateBag.SetValue("MissionTag", "MISSION-42 | PERIMETER-SCAN");

string query1 = "Navigate to original position. Contact me at john.doe@example.com for updates.";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
try
{
  AgentResponse result1 = await motorsAgentWithFullPipeline.RunAsync(query1, session1);
  ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);
}
catch (OperationDeniedException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}

// =============================================================================
// TEST 2: FunctionCalling — illegal reversal blocked before tool execution
// =============================================================================

ColorHelper.PrintColoredLine("""
  ===== TEST 2: FunctionCalling (PreventDangerousMoves + AuditAgentFunctionCalls) =====
  Forward → backward is blocked before the backward tool executes.
  AuditAgentFunctionCalls still logs the invocation and blocked result.
  """);

string query2 = "Move forward 10 meters then go backward 10 meters";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
try
{
  AgentResponse result2 = await motorsAgentWithFullPipeline.RunAsync(query2, session1);
  ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);
}
catch (OperationDeniedException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}

// =============================================================================
// TEST 3: SharedFunction — unauthorized operator blocked before agent runs
// =============================================================================

ColorHelper.PrintColoredLine("""
  ===== TEST 3: Unauthorized operator =====
  OperatorName='observer' fails the SharedFunction gate.
  The agent, response middleware, and tool middleware are never reached.
  """);

AgentSession session2 = await motorsAgentWithFullPipeline.CreateSessionAsync();
session2.StateBag.SetValue("OperatorName", "observer");
session2.StateBag.SetValue("MissionTag", "MISSION-99 | INSPECTION");

string query3 = "Move forward 3 meters";
ColorHelper.PrintColoredLine($"QUERY: {query3}", ConsoleColor.Yellow);
try
{
  AgentResponse result3 = await motorsAgentWithFullPipeline.RunAsync(query3, session2);
  ColorHelper.PrintColoredLine($"\nRESULT: {result3}\n", ConsoleColor.Yellow);
}
catch (OperationDeniedException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}
