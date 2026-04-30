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
// - Human-in-the-loop: Require approval for dangerous operations
// - Agent identity: Knows which agent is calling the function
// - Chainable: Has `next` delegate to continue the pipeline
// - Per-invocation: Runs each time a tool is called
//
// Middleware in this project:
// Existing:
//   - PreventDangerousMoves: Safety approval (existing)
//   - AuditFunctionCalls: Logging with timing (existing)
// New:
//   - AgentConstrainDistanceGate: Evolved constrain with session-aware max (HIGH PRIORITY)
//   - AgentAuditFunctionCalling: Evolved audit with session/tenant tags (HIGH PRIORITY)
//   - AgentToolAllowDenyList: Per-agent tool restrictions
//   - SessionToolBudget: Per-session tool usage counter
//   - ContextAwareToolArgumentFiller: Inject missing args from session
//   - HumanApprovalGateWithChainableAudit: Combined gate + audit
//   - ToolResultPostProcessorWithSessionMemory: Normalize/store tool results
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

  Existing middleware:
  - PreventDangerousMoves: Safety approval
  - AuditFunctionCalls: Logging with timing

  New middleware:
  - AgentConstrainDistanceGate: Session-aware distance clamping (HIGH PRIORITY)
  - AgentAuditFunctionCalling: Rich audit with session/agent context (HIGH PRIORITY)
  - AgentToolAllowDenyList: Per-agent tool restrictions
  - SessionToolBudget: Per-session tool usage counter
  - ContextAwareToolArgumentFiller: Inject missing args from session
  - HumanApprovalGateWithChainableAudit: Combined gate + audit
  - ToolResultPostProcessorWithSessionMemory: Post-process results per agent
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

// =============================================================================
// SCENARIO A: Evolved constrain + audit pattern (HIGH PRIORITY)
// =============================================================================
//
// "We started with anonymous transport-level middleware at ChatClient
//  (ConstrainDistance + AuditFunctionCalling). Once we move the same idea
//  to the Agent layer, we unlock identity: we know which agent, which session,
//  which tenant, and we can change behavior accordingly."
//
// Chain order (outer → inner):
//   AgentToolAllowDenyList      — block disallowed tools early
//   AgentConstrainDistanceGate  — clamp backward distance
//   SessionToolBudget           — enforce per-session usage cap
//   ContextAwareToolArgumentFiller — inject missing args
//   ToolResultPostProcessor     — normalize results per agent
//   AgentAuditFunctionCalling   — log everything (innermost)
// =============================================================================

AIAgent motorsAgentWithAllMiddleware = motorsAgent
  .AsBuilder()
  .Use(AgentFunctionCallings.AgentConstrainDistanceGate)
  .Use(AgentFunctionCallings.SessionToolBudget)
  .Use(AgentFunctionCallings.AgentAuditFunctionCalling)
  .Build();

// =============================================================================
// SCENARIO B: Original PreventDangerousMoves + AuditFunctionCalls (kept)
// =============================================================================

AIAgent motorsAgentWithSafety = motorsAgent
  .AsBuilder()
  .Use(AgentFunctionCallings.PreventDangerousMoves)
  .Use(AgentFunctionCallings.AuditFunctionCalls)
  .Build();

// =============================================================================
// TEST 1: Evolved Constrain + Audit (HIGH PRIORITY)
// =============================================================================
// Forward 10m → no constraint. Backward 8m → constrained to 5m (default).
// "ChatClient.ConstrainDistance is a hardcoded, anonymous, terminal gate.
//  Agent.AgentConstrainDistanceGate reads limits per session and chains via next."
// =============================================================================

AgentSession session1 = await motorsAgent.CreateSessionAsync();

ColorHelper.PrintColoredLine("""
  --- TEST 1: Evolved Constrain + Audit (HIGH PRIORITY) ---
  Forward: unrestricted.  Backward: constrained to 5m (default).
  Full chain: AllowDeny → Constrain → Budget → ArgFill → PostProcess → Audit
  """);

var query1 = "Move forward 10 meters then go backward 8 meters";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
AgentResponse result1 = await motorsAgentWithAllMiddleware.RunAsync(query1, session1);
ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);

// =============================================================================
// TEST 2: Tool AllowDenyList — MotorsAgent only allows motion tools
// =============================================================================

ColorHelper.PrintColoredLine("""
  --- TEST 2: AllowDenyList ---
  MotorsAgent allows only: forward, backward, turn_left, turn_right, stop.
  Any other tool name would be blocked.
  """);

var query2 = "Turn right 45 degrees then stop";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
AgentResponse result2 = await motorsAgentWithAllMiddleware.RunAsync(query2, session1);
ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);

// =============================================================================
// TEST 3: Original Safety Approval (kept, separate pipeline)
// =============================================================================

AgentSession session2 = await motorsAgent.CreateSessionAsync();

ColorHelper.PrintColoredLine("""
  --- TEST 3: Original Safety Approval (PreventDangerousMoves) ---
  Story 3: The Runaway Robot — backward requires human approval!
  """);

var query3 = "Danger ahead! Move backward 10 meters and stop!";
ColorHelper.PrintColoredLine($"QUERY: {query3}", ConsoleColor.Yellow);
AgentResponse result3 = await motorsAgentWithSafety.RunAsync(query3, session2);
ColorHelper.PrintColoredLine($"\nRESULT: {result3}\n", ConsoleColor.Yellow);

// =============================================================================
// KEY INSIGHT: Agent vs ChatClient FunctionCalling
// =============================================================================
ColorHelper.PrintColoredLine("""
  --- CRITICAL DIFFERENCE: Agent vs ChatClient FunctionCalling ---

  Aspect              | Agent FunctionCall    | ChatClient FuncCall
  --------------------|---------------------- |--------------------
  Has `next` delegate | YES (chainable)       | NO (terminal)
  Access to agent     | Yes (agent.Name)      | No
  Session state       | Via context.Arguments | No
  Multiple middlewares| Chain many via .Use() | Single FunctionInvoker
  Execution           | await next(...)       | await InvokeAsync
  Allow/deny lists    | Per-agent             | N/A
  Tool budgets        | Per-session           | N/A
  Argument enrichment | From session context  | N/A

  ChatClient pattern:
    FunctionInvoker = async (ctx, ct) =>
        await ConstrainDistance(ctx, ct)       // terminal gate
          ?? await AuditFunctionCalls(ctx, ct);  // terminal invoker

  Agent pattern:
    .Use(AllowDeny)      // chain: check policy, call next
    .Use(Constrain)      // chain: clamp args, call next
    .Use(Budget)         // chain: count usage, call next
    .Use(ArgFill)        // chain: enrich args, call next
    .Use(PostProcess)    // chain: normalize result, call next
    .Use(Audit)          // chain: log everything, call next

  "We started with anonymous transport-level middleware at ChatClient.
   Once we move the same idea to the Agent layer, we unlock identity:
   we know which agent, which session, which tenant, and we can change
   behavior accordingly."
  """, ConsoleColor.DarkGray);

