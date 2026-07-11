// =============================================================================
// ChatClient SharedFunction Middleware: Universal Pre/Post Processing
// =============================================================================
// 
// PURPOSE: Demonstrate SharedFunction middleware at the ChatClient layer.
// 
// Pattern: Use(Func<messages, options, next, ct, Task>)
// 
// ChatClient SharedFunction middleware is UNIVERSAL - it applies to ALL agents
// using this ChatClient. Unlike Agent SharedFunction:
// - No AgentSession access
// - No agent identity
// - Stateless processing
//
// This is your FAILSAFE layer - even if Agent middleware is misconfigured,
// ChatClient middleware still protects you.
//
// Stories covered:
// - Story 2: The GDPR Nightmare (RemoveEmail — universal failsafe)
// - LimitRequests: request cap (no story label — prevents runaway round-trips)
// =============================================================================

using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Middleware;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

// =============================================================================
// ChatClient with SharedFunction Middleware
// =============================================================================
//
// Pipeline order: STEP 1 of 3 — prepare → handle → invoke
//
// SharedFunction goes OUTERMOST because:
//   ✓ Fail fast: limit checks block before any LLM work begins
//   ✓ Sanitized messages flow down to Response and LLM already clean
//   ✓ Blind to ChatResponse (next returns Task) — input-focused by design
//   ✓ Fires once per LLM round-trip (NOT once per RunAsync)
//      → A single user query with tool calls triggers multiple round-trips;
//        LimitRequests and RemoveEmail re-apply on every round-trip automatically.
// =============================================================================

IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient()
  .AsBuilder()
  // Step 1 of 3: prepare — all fire once per LLM round-trip
  .Use(ChatClientSharedFunctions.LimitRequests)  // request cap — no story label
  .Use(ChatClientSharedFunctions.RemoveEmail)    // Story 2: GDPR Nightmare — sanitize input
  .Build();

Console.OutputEncoding = System.Text.Encoding.UTF8;

ColorHelper.PrintColoredLine("""
  ChatClient SharedFunction Middleware

  Pattern: Use(Func<messages, options, next, ct, Task>)

  UNIVERSAL: Applies to ALL agents using this ChatClient!
  No session, no agent identity - pure stateless processing.

  Stories covered:
  - Story 2: The GDPR Nightmare (RemoveEmail failsafe)
  - LimitRequests: request cap (no story label)
  """, ConsoleColor.DarkGray);

// =============================================================================
// Create agent with protected ChatClient
// =============================================================================

ChatClientAgent motorsAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
  Name = "MotorsAgent",
  Description = "Controls robot car movements.",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
      You have to break down the provided complex commands into the basic moves you know.
      Use a JSON array like [move1, move2, move3] for the response.
      Respond only with the moves and their parameters (angle or distance), without any additional explanations.
      """,
      }
    });

AgentSession session = await motorsAgent.CreateSessionAsync();

// =============================================================================
// TEST 1: Email Removal (Story 2)
// =============================================================================
var query1 = "There is a tree directly in front of the car. Avoid it and then return to the original path.";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
try
{
  var result1 = await motorsAgent.RunAsync(query1, session);
  ColorHelper.PrintColoredLine($"RESULT: {result1}\n", ConsoleColor.Green);
}
catch (LimitExceededException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}

// =============================================================================
// TEST 2: Request Budget (Story 1)
// =============================================================================
var query2 = "Navigate to original position. Contact me at john.doe@example.com for updates.";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
try
{
  var result2 = await motorsAgent.RunAsync(query2, session);
  ColorHelper.PrintColoredLine($"RESULT: {result2}\n", ConsoleColor.Green);
}
catch (LimitExceededException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}

// =============================================================================
// TEST 3: Request Limit (Story 1)
// =============================================================================
var query3 = "Stop and email me at sara.doe@example.com for further instructions.";
ColorHelper.PrintColoredLine($"QUERY: {query3}", ConsoleColor.Yellow);
try
{
  var result3 = await motorsAgent.RunAsync(query3, session);
  ColorHelper.PrintColoredLine($"RESULT: {result3}\n", ConsoleColor.Green);
}
catch (LimitExceededException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}

// =============================================================================
// KEY INSIGHT: ChatClient vs Agent SharedFunction
// =============================================================================
ColorHelper.PrintColoredLine("""
  --- COMPARISON: ChatClient vs Agent SharedFunction ---

  Aspect              | ChatClient          | Agent
  --------------------|---------------------|--------------------
  Scope               | ALL agents          | Per-agent
  Session Access      | No                  | Yes
  Agent Identity      | No                  | No (SharedFunc)
  State               | Stateless           | Session-aware
  Use Case            | Universal policies  | Agent-specific
  Example             | Global Email removal  | Session validation

  ChatClient: Your universal failsafe - protects ALL agents.
  Agent: Fine-grained control with session context.
  """, ConsoleColor.DarkGray);
