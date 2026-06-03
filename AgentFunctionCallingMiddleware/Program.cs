// =============================================================================
// Agent FunctionCalling Middleware: Chainable Tool Invocation Control
// =============================================================================
// 
// PURPOSE: Demonstrate FunctionCalling middleware at the Agent layer.
// 
// Pattern: Use(Func<agent, context, next, ct, ValueTask<object?>>)
// 
// FunctionCalling middleware intercepts tool/function invocations.
// CRITICAL DIFFERENCE from ChatClient: Agent FunctionCalling has `next` delegate!
// This makes it CHAINABLE - you can stack multiple function middlewares.
//
// Key Concepts:
// - Per-invocation: Runs each time a tool is called
// - Agent identity: Knows which agent is calling the function
// - Reads context.Messages: Full chat history including prior tool calls
// - Chainable: Has `next` delegate to continue the pipeline
//
// Middleware in this project:
//   - PreventDangerousMoves: Blocks forward→backward and backward→forward reversals
//   - AuditAgentFunctionCalls: Logging with timing and agent identity
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
  Agent FunctionCalling Middleware

  Pattern: Use(Func<agent, context, next, ct, ValueTask<object?>>)

  CRITICAL: Has `next` delegate — CHAINABLE!
  (Unlike ChatClient FunctionCalling which is TERMINAL)

  Middleware:
  - PreventDangerousMoves: Blocks forward→backward and backward→forward reversals
  - AuditAgentFunctionCalls: Logging with timing and agent identity
  """, ConsoleColor.DarkGray);

// =============================================================================
// Create base agent with tools
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

// Chain order (outer → inner):
//   PreventDangerousMoves — block illegal direction reversals
//   AuditFunctionCalls    — log every tool call with timing
// =============================================================================

AIAgent motorsAgentWithAllMiddleware = motorsAgent
  .AsBuilder()
  .Use(AgentFunctionCallings.PreventDangerousMoves)
  .Use(AgentFunctionCallings.AuditAgentFunctionCalls)
  .Build();

// =============================================================================
// TEST 1: Safe movement — no reversal
// =============================================================================

AgentSession session = await motorsAgent.CreateSessionAsync();

ColorHelper.PrintColoredLine("""
  --- TEST 1: Safe movement (no reversal) ---
  Forward — Audit the movement.
  """);

var query1 = "Move forward 10 meters.";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
AgentResponse result1 = await motorsAgentWithAllMiddleware.RunAsync(query1, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);

// =============================================================================
// TEST 2: Illegal movement with no stop in between
// =============================================================================

ColorHelper.PrintColoredLine("""
  --- TEST 2: Illegal movement with no stop in between ---
  Forward → backward — PreventDangerousMoves blocks the reversal.
  """);

var query2 = "Move forward 2 meters and immediatelly move backward 2 meters.";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
AgentResponse result2 = await motorsAgentWithAllMiddleware.RunAsync(query2, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);

ColorHelper.PrintColoredLine("""
  --- CRITICAL DIFFERENCE: Agent vs ChatClient FunctionCalling ---

  Aspect              | Agent FunctionCall    | ChatClient FuncCall
  --------------------|---------------------- |--------------------
  Has `next` delegate | YES (chainable)       | NO (terminal)
  Access to agent     | Yes (agent.Name)      | No
  context.Messages    | Full chat history     | No
  Multiple middlewares| Chain many via .Use() | Single FunctionInvoker
  Execution           | await next(...)       | await InvokeAsync

  Agent pattern:
    .Use(PreventDangerousMoves)     // read context.Messages, block reversal, call next
    .Use(AuditAgentFunctionCalls)   // log timing, call next

  "context.Messages gives FunctionCalling middleware the full chat history,
   including all prior tool calls — no need to track state manually."
  """, ConsoleColor.DarkGray);

