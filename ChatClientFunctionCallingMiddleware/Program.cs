// =============================================================================
// ChatClient FunctionCalling Middleware: Terminal Invocation
// =============================================================================
// 
// PURPOSE: Demonstrate FunctionCalling middleware at the ChatClient layer.
// 
// Pattern: UseFunctionInvocation with FunctionInvoker delegate
// 
// CRITICAL DIFFERENCE FROM AGENT: NO `next` DELEGATE!
// 
// Agent FunctionCalling:      Has `next` → CHAINABLE
// ChatClient FunctionCalling: No `next`  → TERMINAL
// 
// You MUST call context.Function.InvokeAsync() directly.
// You CANNOT chain multiple middlewares.
//
// This is UNIVERSAL - applies to function calls from ANY agent.
// =============================================================================

using AITools;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

// =============================================================================
// ChatClient with FunctionCalling Middleware
// =============================================================================
//
// Pipeline order: STEP 3 of 3 — prepare → handle → invoke
//
// FunctionCalling MUST be innermost (.UseFunctionInvocation last before .Build()) because:
//   ✓ Its internal tool loop is invisible to SharedFunction and Response
//      → Both outer layers see ONE activation per GetResponseAsync call
//   ⚠ If placed outermost, SharedFunction and Response fire once per internal
//      LLM round-trip instead of once per external call — every tool call
//      triggers a full re-run of budget checks, sanitization, and timing.
//
// TERMINAL: no next delegate — compose gate + invoker manually in FunctionInvoker.
//   Double-invocation trap: two methods each calling InvokeAsync = tool fires twice.
//   Gate pattern solution: ConstrainDistance (gate, no InvokeAsync) runs first;
//   AuditFunctionCalls (terminal invoker, owns InvokeAsync) runs second.
//
// Full recommended pipeline when combining all three types:
//   .Use(SharedFunction.LimitRequests)              // 1. prepare — fail fast
//   .Use(SharedFunction.RemoveEmail)                // 1. prepare — sanitize (Story 2: GDPR)
//   .Use(Response.EnforceTokenBudget, null)         // 2. handle — token budget (Story 1: $10,847)
//   .Use(Response.AddTimestamp, null)               // 2. handle — timestamps
//   .UseFunctionInvocation(...)                     // 3. invoke — always last (Story 3: ConstrainDistance)
// =============================================================================

IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient()
  .AsBuilder()

  // Step 3 of 3: invoke — MUST be last before .Build()
  .UseFunctionInvocation(loggerFactory: null, configure: options =>
  {
    // Transform pattern: ConstrainDistance modifies arguments in-place and always returns null.
    // ?? short-circuits on non-null; ConstrainDistance always returns null — falls through to AuditFunctionCalls.
    options.FunctionInvoker = async (context, ct) =>
    {
      var response = await Middleware.ChatClientFunctionCallings.ConstrainDistance(context, ct)
        ?? await Middleware.ChatClientFunctionCallings.AuditFunctionCalling(context, ct);
      return response;
    };
  })
  .Build();

Console.OutputEncoding = System.Text.Encoding.UTF8;

ColorHelper.PrintColoredLine("""
  ChatClient FunctionCalling Middleware

  CRITICAL: NO `next` DELEGATE - TERMINAL EXECUTION!

  Agent:      .Use(middleware).Use(middleware) -> chains via `next`
  ChatClient: FunctionInvoker = async (ctx, ct) => InvokeAsync()

  This is UNIVERSAL - applies to ALL agents' function calls!
  """, ConsoleColor.DarkGray);

// =============================================================================
// Create agent with tools
// =============================================================================

ChatClientAgent motorsAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
  Name = "MotorsAgent",
  Description = "Controls robot car movements.",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      You are the MotorsAgent controlling a robot car.
      Break down complex movement commands into basic moves: forward, backward, turn left, turn right, stop.
      Respond with the sequence of moves needed to accomplish the task.
      """,
    Tools = [.. MotorTools.AsAITools()],
  }
});

AgentSession session = await motorsAgent.CreateSessionAsync();

// =============================================================================
// TEST: ConstrainDistance (backward only, 5m max) + Audit
// =============================================================================
// Forward 10m  → no constraint applied (forward is unrestricted)
// Backward 8m  → constrained to 5m
// (Notice: No agent name in logs - ChatClient doesn't know!)
ColorHelper.PrintColoredLine("""
  Forward: unrestricted
  Backward: constrained to 5m max
  """);

string query = "Move forward 10 meters then go backward 8 meters";
ColorHelper.PrintColoredLine($"QUERY: {query}", ConsoleColor.Yellow);
AgentResponse result = await motorsAgent.RunAsync(query, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result}\n", ConsoleColor.Yellow);

// =============================================================================
// CRITICAL: Agent vs ChatClient FunctionCalling
// =============================================================================
ColorHelper.PrintColoredLine("""
  CRITICAL DIFFERENCE: Agent vs ChatClient FunctionCalling

  Aspect              | Agent               | ChatClient
  --------------------|---------------------|--------------------
  Has `next` delegate | YES                 | NO
  Chainable           | Multiple            | Single
  Agent Identity      | agent.Name          | None
  Execution           | await next(...)     | await InvokeAsync
  Pattern             | Middleware chain    | Terminal invoker

  Agent Pattern:
    .Use(ConstrainArgs)    // transforms arguments, calls next(context, ct)
    .Use(AuditCalls)       // audits result, calls next(context, ct)
    -> Both run in sequence via `next` delegate

  ChatClient Pattern:
    FunctionInvoker = async (ctx, ct) =>
        await ConstrainDistance(ctx, ct)        // Story 3: transforms args, returns null
          ?? await AuditFunctionCalling(ctx, ct);  // owns InvokeAsync
  """, ConsoleColor.DarkGray);
