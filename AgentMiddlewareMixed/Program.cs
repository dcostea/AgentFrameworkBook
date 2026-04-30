// =============================================================================
// Agent Middleware Mixed: All Three Middleware Types Combined
// =============================================================================
//
// PURPOSE: Demonstrate the full Agent middleware pipeline — SharedFunction,
//          Response, and FunctionCalling — composed into a single agent.
//
// Pipeline (outer → inner):
//   SharedFunction (Prepare)  — runs before and after the agent
//   Response (Handle)         — intercepts the request/response cycle
//   FunctionCalling (Invoke)  — intercepts every tool invocation
//
// Registration order (outer → inner):
//   SharedFunction:    SessionScopedLimitRequests → TenantGuardrails → FeatureFlagBootstrap → PersistentRemoveEmail
//   Response:          AgentRunAnalyticsEnvelope → AuditConversation → PersonaAlignedErrorResponse → CaptainsLog → MeasureResponseTime
//   FunctionCalling:   ConstrainDistance → SessionToolBudget → AuditFunctionCalling
//
// The two structural differences from ChatClient (MiddlewareMixed):
//   1. Agent middleware fires once per RunAsync, not once per LLM round-trip.
//      Token budgets and rate limiting at ChatClient count every round-trip;
//      here SessionScopedLimitRequests counts agent runs per session.
//   2. FunctionCalling is CHAINABLE — each method calls next, so the framework
//      invokes the tool exactly once at the end of the chain. No gate pattern needed.
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

  SharedFunction → Response → FunctionCalling

  SharedFunction (Prepare):
    1. SessionScopedLimitRequests — per-session run budget (evolved from ChatClient.LimitRequests)
    2. TenantGuardrails           — inject policy system messages per tenant/region
    3. FeatureFlagBootstrap       — resolve session-local feature flags
    4. PersistentRemoveEmail      — persistent GDPR sanitization (evolved from ChatClient.RemoveEmail)

  Response (Handle):
    5. AgentRunAnalyticsEnvelope  — telemetry with agent/session identity
    6. AuditConversation          — full audit with agent.Name and session id
    7. PersonaAlignedErrorResponse— persona-specific error messages on failure
    8. CaptainsLog                — journal prefix with agent.Name (evolved from ChatClient.AddTimestamp)
    9. MeasureResponseTime        — full RunAsync timing (not per round-trip)

  FunctionCalling (Invoke):
   10. ConstrainDistance          — session-aware backward distance clamping (evolved from ChatClient.ConstrainDistance)
   11. SessionToolBudget          — per-session per-tool usage counter
   12. AuditFunctionCalling       — chainable tool audit with agent.Name
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
  .Use(sharedFunc: AgentSharedFunctions.SessionScopedLimitRequests)
  .Use(sharedFunc: AgentSharedFunctions.TenantGuardrails)
  .Use(sharedFunc: AgentSharedFunctions.FeatureFlagBootstrap)
  .Use(sharedFunc: AgentSharedFunctions.PersistentRemoveEmail)
  // Response — Handle layer
  .Use(runFunc: AgentResponses.AgentRunAnalyticsEnvelope, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.AuditConversation, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.PersonaAlignedErrorResponse, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.CaptainsLog, runStreamingFunc: null)
  .Use(runFunc: AgentResponses.MeasureResponseTime, runStreamingFunc: null)
  // FunctionCalling — Invoke layer (innermost)
  .Use(AgentFunctionCallings.ConstrainDistance)
  .Use(AgentFunctionCallings.SessionToolBudget)
  .Use(AgentFunctionCallings.AuditFunctionCalling)
  .Build();

// =============================================================================
// TEST 1: SharedFunction — email sanitization + tenant guardrails
// =============================================================================

ColorHelper.PrintColoredLine("""
  ===== TEST 1: SharedFunction (PersistentRemoveEmail + TenantGuardrails) =====
  (Without middleware: email reaches the LLM — GDPR violation; no tenant guardrails)
  (With middleware: email redacted; EU guardrail injected before the agent sees the query)
  """);

AgentSession session1 = await motorsAgentWithFullPipeline.CreateSessionAsync();
session1.StateBag.SetValue("TenantId", "acme-corp");
session1.StateBag.SetValue("Region", "EU");
session1.StateBag.SetValue("AgentName", "MotorsAgent");
session1.StateBag.SetValue("Environment", "production");
session1.StateBag.SetValue("UserId", "operator-1");

string query1 = "Navigate to original position. Contact me at john.doe@example.com for updates.";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
try
{
  AgentResponse result1 = await motorsAgentWithFullPipeline.RunAsync(query1, session1);
  ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);
}
catch (SessionLimitExceededException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}

// =============================================================================
// TEST 2: FunctionCalling — distance clamping + audit
// =============================================================================

ColorHelper.PrintColoredLine("""
  ===== TEST 2: FunctionCalling (ConstrainDistance + SessionToolBudget + AuditFunctionCalling) =====
  (Without middleware: backward runs the full 10m — no constraint, no audit)
  (With middleware: backward constrained to 5m; every tool call logged with agent.Name)
  """);

string query2 = "Move forward 10 meters then go backward 10 meters";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
try
{
  AgentResponse result2 = await motorsAgentWithFullPipeline.RunAsync(query2, session1);
  ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);
}
catch (SessionLimitExceededException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}

// =============================================================================
// TEST 3: Response — Captain's Log + analytics
// =============================================================================

ColorHelper.PrintColoredLine("""
  ===== TEST 3: Response (CaptainsLog + AgentRunAnalyticsEnvelope + MeasureResponseTime) =====
  (Without middleware: no journal prefix, no telemetry, no timing)
  (With middleware: "Captain's log. Stardate..." prefix; analytics envelope; full RunAsync timing)
  """);

string query3 = "Move forward 3 meters, turn right 90 degrees, move forward 3 meters";
ColorHelper.PrintColoredLine($"QUERY: {query3}", ConsoleColor.Yellow);
try
{
  AgentResponse result3 = await motorsAgentWithFullPipeline.RunAsync(query3, session1);
  ColorHelper.PrintColoredLine($"\nRESULT: {result3}\n", ConsoleColor.Yellow);
}
catch (SessionLimitExceededException ex)
{
  ColorHelper.PrintColoredLine($"EXCEPTION: {ex.Message}\n", ConsoleColor.Red);
}
