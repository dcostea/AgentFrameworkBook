// =============================================================================
// ChatClient Response Middleware: Pure LLM Processing
// =============================================================================
// 
// PURPOSE: Demonstrate Response middleware at the ChatClient layer.
// 
// Pattern: Use(Func<messages, options, innerClient, ct, Task<ChatResponse>>, null)
// 
// ChatClient Response middleware intercepts the LLM request/response cycle.
// Unlike Agent Response middleware:
// - No AgentSession access
// - No agent identity (no innerAgent.Name)
// - Measures PURE LLM latency (not agent orchestration time)
//
// This is universal - applies to ALL agents using this ChatClient.
//
// Middleware in this project (the "2" in 2+2+2):
// - EnforceTokenBudget: Token budget enforcement (Story 1: token consumption spirals unchecked into a $10,847 bill)
// - AddTimestamp: Datetime-stamp every response (board journal)
// =============================================================================

using AITools;
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
// ChatClient with Response Middleware
// =============================================================================
//
// Pipeline order: STEP 2 of 3 — prepare → handle → invoke
//
// Response goes AFTER SharedFunction because:
//   ✓ Receives already-sanitized messages — Response sees clean input
//   ✓ Measures only LLM work — SharedFunction preprocessing time is excluded
//   ✓ Sees ChatResponse: exclusive access to response.Usage (token counts)
//   ✓ Fires once per LLM round-trip — wraps the full LLM exchange entry-to-exit
//
// Registration order within Response:
//   EnforceTokenBudget OUTER → AddTimestamp INNER
//   EnforceTokenBudget pre-flight guard runs first — budget exhaustion skips AddTimestamp
//   AddTimestamp stamps the response text after the innerClient.GetResponseAsync() call
// =============================================================================

IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient()
  .AsBuilder()
  // Step 2 of 3: handle — both fire once per LLM round-trip
  // Second parameter null = no streaming handler
  .Use(ChatClientResponses.EnforceTokenBudget, null)
  .Use(ChatClientResponses.AddTimestamp, null)
  .Build();

Console.OutputEncoding = System.Text.Encoding.UTF8;

ColorHelper.PrintColoredLine("""
  ChatClient Response Middleware

  Pattern: Use(Func<..., innerClient, ..., ChatResponse>, null)

  UNIVERSAL: Applies to ALL agents using this ChatClient!
  No session, no agent identity - pure LLM processing.

  Middleware (the '2' in 2+2+2):
  - EnforceTokenBudget: Token budget enforcement (Story 1: $10,847 bill)
  - AddTimestamp: Datetime-stamp every response (board journal)
  """, ConsoleColor.DarkGray);

// =============================================================================
// Create agent
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
// TEST 1: LLM Audit and Metrics
// =============================================================================
var query1 = "Move forward 3 meters";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
var result1 = await motorsAgent.RunAsync(query1, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);

// =============================================================================
// TEST 2: Pure LLM Latency vs Agent Execution Time
// =============================================================================
var query2 = "Turn right 45 degrees";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
var result2 = await motorsAgent.RunAsync(query2, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);

// =============================================================================
// KEY INSIGHT: ChatClient vs Agent Response
// =============================================================================
ColorHelper.PrintColoredLine("""
  --- COMPARISON: ChatClient vs Agent Response Middleware ---

  Aspect              | ChatClient          | Agent
  --------------------|---------------------|--------------------
  Scope               | ALL agents          | Per-agent
  Access to           | innerClient         | innerAgent
  Agent Identity      | No                  | Yes (Name)
  Session             | No                  | Yes
  Latency measures    | Pure LLM            | Full execution
  Return type         | ChatResponse        | AgentResponse

  ChatClient: Measures pure LLM latency (typically 200-2000ms)
  Agent: Measures full execution including tools (can be 5-10x longer)
  """, ConsoleColor.DarkGray);
