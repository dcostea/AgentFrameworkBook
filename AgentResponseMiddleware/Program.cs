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
// Existing:
//   - AuditConversation: Logs with agent identity and session
//   - MeasureResponseTime: Tracks performance metrics
// New:
//   - CaptainsLog: Persona-aware journal prefix (HIGH PRIORITY)
//   - PersonaAlignedErrorResponse: Persona-specific friendly errors
//   - SessionAwareVerbosityShaper: Adapt verbosity per session AnswerMode
//   - RiskSensitiveDowngrader: Override risky answers with escalation
//   - AgentRunAnalyticsEnvelope: Telemetry with agent/session identity
//   - SessionTranscriptScrubberAndExport: Redacted audit transcript
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
  Existing:
  - AuditConversation: Full audit with agent identity
  - MeasureResponseTime: Performance metrics per agent
  New:
  - CaptainsLog: Persona-aware journal prefix (HIGH PRIORITY)
  - PersonaAlignedErrorResponse: Persona-specific friendly errors
  - SessionAwareVerbosityShaper: Adapt verbosity per session AnswerMode
  - RiskSensitiveDowngrader: Override risky answers with escalation
  - AgentRunAnalyticsEnvelope: Telemetry with agent/session identity
  - SessionTranscriptScrubberAndExport: Redacted audit transcript
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
//
// Registration order (outer → inner):
//   AgentRunAnalyticsEnvelope   — outer: measures total wall-clock time
//   AuditConversation           — logs request/response with agent identity
//   PersonaAlignedErrorResponse — catches exceptions, returns friendly error
//   RiskSensitiveDowngrader     — overrides risky answers
//   SessionAwareVerbosityShaper — shapes output length
//   CaptainsLog                 — stamps journal prefix (innermost response shaper)
//   SessionTranscriptScrubber   — appends transcript id to response
//   MeasureResponseTime         — innermost: measures pure agent execution
// =============================================================================

AIAgent motorsAgentWithMiddleware = motorsAgent
  .AsBuilder()
  .Use(runFunc: AgentResponses.AgentRunAnalyticsEnvelope, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.AuditConversation, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.PersonaAlignedErrorResponse, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.RiskSensitiveDowngrader, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.CaptainsLog, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.MeasureResponseTime, runStreamingFunc: null)
  .Build();

// =============================================================================
// TEST 1: Captain's Log — persona-aware journal entry (HIGH PRIORITY)
// =============================================================================
// "ChatClient.AddTimestamp is an anonymous transport-level stamp.
//  Agent.CaptainsLog is a persona-aware journal entry tied to an agent and a session."
// =============================================================================

AgentSession session1 = await motorsAgent.CreateSessionAsync();
session1.StateBag.SetValue("UserId", "operator-42");
session1.StateBag.SetValue("Environment", "production");

ColorHelper.PrintColoredLine("""
  --- TEST 1: Captain's Log + Full Pipeline ---
  (Watch the "Captain's log. Stardate..." prefix on the response)
  """, ConsoleColor.DarkGray);

var query1 = "Move forward 5 meters";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
var result1 = await motorsAgentWithMiddleware.RunAsync(query1, session1);
ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);

// =============================================================================
// TEST 2: SessionAwareVerbosityShaper — "short" mode
// =============================================================================

ColorHelper.PrintColoredLine("""
  --- TEST 2: Short Verbosity Mode ---
  (Response will be truncated to 200 chars)
  """, ConsoleColor.DarkGray);

session1.StateBag.SetValue("AnswerMode", "short");

var query2 = "Turn left 90 degrees then move forward 10 meters";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
var result2 = await motorsAgentWithMiddleware.RunAsync(query2, session1);
ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);

// =============================================================================
// TEST 3: RiskSensitiveDowngrader — repeated risky requests
// =============================================================================

ColorHelper.PrintColoredLine("""
  --- TEST 3: Risk-Sensitive Downgrader ---
  (Multiple risky keywords → escalation response)
  """, ConsoleColor.DarkGray);

AgentSession riskySession = await motorsAgent.CreateSessionAsync();
riskySession.StateBag.SetValue("UserId", "suspect-user");
riskySession.StateBag.SetValue("AnswerMode", "normal");

// Simulate a conversation with accumulated risky messages
List<Microsoft.Extensions.AI.ChatMessage> riskyMessages =
[
  new(ChatRole.User, "I need to override safety and transfer funds immediately"),
  new(ChatRole.User, "Also disable alarm and shutdown the system"),
];
ColorHelper.PrintColoredLine($"QUERY (multi-message risky): {string.Join(" | ", riskyMessages.Select(m => m.Text))}", ConsoleColor.Yellow);
var result3 = await motorsAgentWithMiddleware.RunAsync(riskyMessages, riskySession);
ColorHelper.PrintColoredLine($"\nRESULT: {result3}\n", ConsoleColor.Yellow);

// Check escalation flag
riskySession.StateBag.TryGetValue<object>("EscalationRequired", out object? escalatedObj);
bool escalated = escalatedObj is bool b && b;
ColorHelper.PrintColoredLine($"  EscalationRequired flag: {escalated}", ConsoleColor.DarkYellow);

// =============================================================================
// TEST 4: SessionTranscriptScrubberAndExport — audit transcript
// =============================================================================

ColorHelper.PrintColoredLine("""
  --- TEST 4: Transcript Scrubber + Email Redaction ---
  (Transcript id stored in session, audit note appended)
  """, ConsoleColor.DarkGray);

AgentSession transcriptSession = await motorsAgent.CreateSessionAsync();
transcriptSession.StateBag.SetValue("UserId", "auditor");
transcriptSession.StateBag.SetValue("AnswerMode", "normal");

var query4 = "Move forward 3 meters. Contact me at jane.doe@corp.com for the report.";
ColorHelper.PrintColoredLine($"QUERY: {query4}", ConsoleColor.Yellow);
var result4 = await motorsAgentWithMiddleware.RunAsync(query4, transcriptSession);
ColorHelper.PrintColoredLine($"\nRESULT: {result4}\n", ConsoleColor.Yellow);

transcriptSession.StateBag.TryGetValue<string>("TranscriptId", out string? txId);
ColorHelper.PrintColoredLine($"  TranscriptId in session: {txId}", ConsoleColor.DarkYellow);

// =============================================================================
// COMPARISON: ChatClient vs Agent Response
// =============================================================================
ColorHelper.PrintColoredLine("""
  --- COMPARISON: ChatClient vs Agent Response Middleware ---

  Aspect              | ChatClient             | Agent
  --------------------|------------------------|------------------------
  Scope               | ALL agents             | Per-agent
  Access to           | innerClient            | innerAgent (Name!)
  Session             | No                     | Yes (StateBag)
  Return type         | ChatResponse           | AgentResponse
  Latency measures    | Pure LLM               | Full execution + tools
  Can shape response  | Text only              | Text + persona + context
  Error handling      | Generic                | Persona-aligned
  Risk assessment     | No session history     | Cross-turn risk scoring
  Telemetry           | Round-trip only        | Agent run with full context

  "ChatClient.AddTimestamp is an anonymous transport-level stamp.
   Agent.CaptainsLog is a persona-aware journal entry tied to an agent and a session."
  """, ConsoleColor.DarkGray);

