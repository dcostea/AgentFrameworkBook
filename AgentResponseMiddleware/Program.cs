// =============================================================================
// Agent Response Middleware: Use(runFunc: delegate)
// =============================================================================
// 
// PURPOSE: Demonstrate Response middleware at the Agent layer.
// 
// Pattern: Use(runFunc: Func<messages, session, options, innerAgent, ct, Task<AgentResponse>>)
// 
// Response middleware intercepts the entire request/response cycle.
// Unlike SharedFunction, you have access to:
// - The innerAgent (including agent.Name!)
// - The AgentResponse to inspect or modify
//
// Key Concepts:
// - Full request/response interception
// - Access to agent identity (innerAgent.Name)
// - Can modify or replace the response
// - Perfect for auditing, metrics, caching, filtering
//
// Middleware in this project:
//   - CaptainsLog: Persona-aware journal prefix (evolved from ChatClient.AddTimestamp)
//   - MovementSequenceAuditor: Detects illegal direction reversals in the executed sequence
// =============================================================================

using AITools;
using Helpers;
using Middleware;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

Console.OutputEncoding = System.Text.Encoding.UTF8;

ColorHelper.PrintColoredLine("""
  Agent Response Middleware

  Pattern: Use(runFunc: delegate, runStreamingFunc: null)

  Response middleware intercepts the request/response cycle.
  Key feature: Access to innerAgent.Name!

  Middleware:
  - CaptainsLog: Persona-aware journal prefix (evolved from ChatClient.AddTimestamp)
  - MovementSequenceAuditor: Detects illegal direction reversals in the executed sequence
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
// RESPONSE MIDDLEWARE: Use(runFunc: delegate, runStreamingFunc: null)
// =============================================================================

AIAgent motorsAgentWithMiddleware = motorsAgent
  .AsBuilder()
  .Use(runFunc: AgentResponses.MovementSequenceAuditor, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.CaptainsLog, runStreamingFunc: null)
  .Build();

// =============================================================================
// TEST 1: Captain's Log — persona-aware journal entry
// =============================================================================

AgentSession session1 = await motorsAgent.CreateSessionAsync();
session1.StateBag.SetValue("Environment", "production");

ColorHelper.PrintColoredLine("""
  --- TEST 1: Captain's Log ---
  (Watch the "Captain's log. Stardate..." prefix on the response)
  """, ConsoleColor.DarkGray);

var query1 = "Move forward 5 meters";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
var result1 = await motorsAgentWithMiddleware.RunAsync(query1, session1);
ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);

// =============================================================================
// TEST 2: Different session — no Environment tag in journal entry
// =============================================================================

ColorHelper.PrintColoredLine("""
  --- TEST 2: No Environment tag ---
  (Journal entry omits the Env tag when StateBag has no Environment)
  """, ConsoleColor.DarkGray);

AgentSession session2 = await motorsAgent.CreateSessionAsync();

var query2 = "Turn left 90 degrees";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
var result2 = await motorsAgentWithMiddleware.RunAsync(query2, session2);
ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);

// =============================================================================
// COMPARISON: ChatClient vs Agent Response
// =============================================================================
ColorHelper.PrintColoredLine("""
  --- COMPARISON: ChatClient vs Agent Response Middleware ---

  ChatClient.AddTimestamp  →  anonymous bare UTC stamp on every response
  Agent.CaptainsLog        →  persona-aware journal entry with agent name,
                               session id, and per-session StateBag context

  ChatClient has no agent identity or session — it is transport-level only.
  Agent Response middleware operates at the run level with full context.
  """, ConsoleColor.DarkGray);


