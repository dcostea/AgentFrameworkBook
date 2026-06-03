// =============================================================================
// Agent SharedFunction Middleware: Use(sharedFunc: delegate)
// =============================================================================
// 
// PURPOSE: Demonstrate SharedFunction middleware at the Agent layer.
// 
// Pattern: Use(sharedFunc: Func<messages, session, options, next, ct, Task>)
// 
// SharedFunction middleware executes BEFORE and AFTER the agent runs.
// It's the delegate-based equivalent of the factory pattern (7.03), but
// without needing to write a custom AIAgent class.
//
// Key Concepts:
// - Pre-processing: Validate, transform, or enrich before execution
// - Post-processing: Cleanup, logging, or finalization after execution
// - The `next` delegate: Calls the next middleware (or the agent)
// - Session access: Has AgentSession context (StateBag for per-session state)
//
// Middleware in this project:
// 1. PersistentRemoveEmail: Persistent email sanitization (evolved from ChatClient.RemoveEmail)
// 2. AgentGuardrails: OperatorName gate + MissionTag message prefix (new)
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

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

Console.OutputEncoding = System.Text.Encoding.UTF8;

ColorHelper.PrintColoredLine("""
  Agent SharedFunction Middleware

  Pattern: Use(sharedFunc: delegate)

  SharedFunction executes BEFORE and AFTER the agent runs.
  Key feature: AgentSession access (StateBag for per-session state)

  Middleware:
  1. PersistentRemoveEmail — persistent email sanitization (GDPR)
  2. AgentGuardrails — OperatorName gate + MissionTag prefix (StateBag)
  """, ConsoleColor.DarkGray);

// =============================================================================
// Create base agent with ALL SharedFunction middlewares
// =============================================================================
//
// ORDER MATTERS: outermost middleware fires first (pre) and last (post).
//   1. AgentGuardrails — gate on OperatorName, prefix MissionTag onto user messages
//   2. PersistentRemoveEmail — sanitize input last (closest to agent)
// =============================================================================

AIAgent motorsAgent = new OpenAIClient(apiKey)
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
  })
  .AsBuilder()
  .Use(sharedFunc: AgentSharedFunctions.AgentGuardrails)
  .Use(sharedFunc: AgentSharedFunctions.PersistentRemoveEmail)
  .Build();

// =============================================================================
// Create session and pre-populate StateBag for demo
// =============================================================================

AgentSession session = await motorsAgent.CreateSessionAsync();

// Pre-populate session state for AgentGuardrails
session.StateBag.SetValue("OperatorName", "driver");
session.StateBag.SetValue("MissionTag", "MISSION-42 | PERIMETER-SCAN");

// =============================================================================
// TEST 1: Full pipeline — email, tenant, flags, language, limit
// =============================================================================
ColorHelper.PrintColoredLine("""
  --- TEST 1: Authorised operator, mission tag prefixed ---
  operator='driver' passes the gate; '[MISSION-42 | PERIMETER-SCAN]' prefixed to query.
  """);
var query1 = "Navigate forward 5 meters. My email is john.doe@example.com if needed.";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
try
{
  var result1 = await motorsAgent.RunAsync(query1, session);
  ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);
}
catch (OperationDeniedException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}
// TEST 2: Second run

ColorHelper.PrintColoredLine("""
  --- TEST 2: Second Run (session counter = 2, same operator and mission) ---
  """);
var query2 = "Turn left 90 degrees.";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
try
{
  var result2 = await motorsAgent.RunAsync(query2, session);
  ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);
}
catch (OperationDeniedException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}

// =============================================================================
// TEST 3:
// =============================================================================
ColorHelper.PrintColoredLine("""
  --- TEST 3: Unauthorised operator ---
  operator='observer' is not 'driver' — AgentGuardrails throws before agent runs.
  """);

session.StateBag.SetValue("OperatorName", "observer");

var query3 = "Move forward 3 meters.";
ColorHelper.PrintColoredLine($"QUERY: {query3}", ConsoleColor.Yellow);
try
{
  var result3 = await motorsAgent.RunAsync(query3, session);
  ColorHelper.PrintColoredLine($"\nRESULT: {result3}\n", ConsoleColor.Yellow);
}
catch (OperationDeniedException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}

// =============================================================================
// EXPLANATION:
// =============================================================================
ColorHelper.PrintColoredLine("""
  HOW SHAREDFUNC MIDDLEWARE WORKS (with 2 middlewares)

  User Request
    |
  AgentGuardrails PRE — gate on OperatorName, prefix MissionTag
    |
  PersistentRemoveEmail PRE — sanitize emails from input
    |
  AGENT EXECUTION
    |
  PersistentRemoveEmail POST
    |
  AgentGuardrails POST
    |
  Response

  The `next` delegate is the key — it continues the pipeline.
  Everything before `await next(...)` is PRE-processing.
  Everything after `await next(...)` is POST-processing.

  KEY DIFFERENCE FROM CHATCLIENT:
  - ChatClient SharedFunction: no session, no tenant, no flags — app-wide only
  - Agent SharedFunction: session.StateBag enables per-user, per-tenant, per-agent state
  """, ConsoleColor.DarkGray);

