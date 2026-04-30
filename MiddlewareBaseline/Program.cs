// =============================================================================
// BASELINE: Robot Car Agent WITHOUT ChatClient Middleware
// =============================================================================
//
// PURPOSE: Demonstrate the "naked" ChatClient — no middleware pipeline at all.
//
// The three companion projects each add one layer of the 3-step pipeline:
//
//   Step 1 — prepare  (ChatClientSharedFunctionMiddleware)
//     · LimitRequests   → request cap (no story label)
//     · RemoveEmail     → Story 2: The GDPR Nightmare  (email sanitization)
//
//   Step 2 — handle   (ChatClientResponseMiddleware)
//     · EnforceTokenBudget → Story 1: token consumption spirals unchecked into a $10,847 bill
//     · AddTimestamp       → Datetime-stamp every response
//
//   Step 3 — invoke   (ChatClientFunctionCallingMiddleware)
//     · ConstrainDistance     → Story 3: constrain backward distance (capped to 5 m)
//     · AuditFunctionCalling  → Timestamp + argument + timing audit
//
// This project shows what happens when NONE of those layers exist.
// Every problem the middleware projects solve is visible here.
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
// ChatClient WITHOUT any middleware
// =============================================================================
//
// No .AsBuilder() — no pipeline. Every gap the 3-step middleware fills is open:
//   Step 1 (prepare): no request limit, no email removal
//   Step 2 (handle):  no token budget, no response timestamps
//   Step 3 (invoke):  no distance constraints, no function-call audit
// =============================================================================

IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

Console.OutputEncoding = System.Text.Encoding.UTF8;

ColorHelper.PrintColoredLine("""
  BASELINE: Robot Car Agent WITHOUT ChatClient Middleware

  No .AsBuilder() — no pipeline. All three steps are missing:

  Step 1 — prepare  (ChatClientSharedFunctionMiddleware)
    · No request limit   → Story 1: The $10,000 Weekend
    · No email removal   → Story 2: The GDPR Nightmare

  Step 2 — handle   (ChatClientResponseMiddleware)
    · No token budget    → unlimited token spend
    · No timestamps      → no response audit trail

  Step 3 — invoke   (ChatClientFunctionCallingMiddleware)
    · No distance cap    → backward runs unconstrained
    · No function audit  → no tool-call logging
  """, ConsoleColor.DarkGray);

// =============================================================================
// Create agent — identical to the middleware projects
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
// TEST 1: Email leaks to LLM (Step 1 gap — no RemoveEmail)
// =============================================================================
// ChatClientSharedFunctionMiddleware.RemoveEmail would redact the address.
// Here the email travels straight to the LLM provider — a GDPR violation.
ColorHelper.PrintColoredLine("""
  TEST 1: Email Leak — Step 1 gap (no RemoveEmail)
  ChatClientSharedFunctionMiddleware would redact the address.
  """);

var query1 = "Navigate to original position. Contact me at john.doe@example.com for updates.";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
AgentResponse result1 = await motorsAgent.RunAsync(query1, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);
ColorHelper.PrintColoredLine("""
  >>> The email was NOT redacted — sent directly to the LLM provider (GDPR violation).
  """, ConsoleColor.DarkYellow);

// =============================================================================
// TEST 2: No request or token budget (Step 1 + Step 2 gap)
// =============================================================================
// ChatClientSharedFunctionMiddleware.LimitRequests would cap LLM round-trips.
// ChatClientResponseMiddleware.EnforceTokenBudget would cap cumulative tokens.
// Here neither guard exists — every query consumes resources unchecked.
ColorHelper.PrintColoredLine("""
  TEST 2: Unlimited Spend — Step 1 + Step 2 gap (no LimitRequests, no EnforceTokenBudget)
  ChatClientSharedFunctionMiddleware would limit requests.
  ChatClientResponseMiddleware would enforce a token budget.
  """);

var query2 = "Move forward 3 meters, turn right 90 degrees, move forward 3 meters";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
AgentResponse result2 = await motorsAgent.RunAsync(query2, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);
ColorHelper.PrintColoredLine("""
  >>> No request limit hit, no token budget enforced — costs accumulate silently.
  """, ConsoleColor.DarkYellow);

// =============================================================================
// TEST 3: Backward runs unconstrained (Step 3 gap — no ConstrainDistance)
// =============================================================================
// ChatClientFunctionCallingMiddleware.ConstrainDistance would cap backward to 5 m.
// ChatClientFunctionCallingMiddleware.AuditFunctionCalling would log every tool call.
// Here the robot reverses the full 10 m with no audit trail.
ColorHelper.PrintColoredLine("""
  TEST 3: Unconstrained Backward — Step 3 gap (no ConstrainDistance, no AuditFunctionCalling)
  ChatClientFunctionCallingMiddleware would cap backward to 5 m and audit every tool call.
  """);

var query3 = "Move forward 10 meters then go backward 10 meters";
ColorHelper.PrintColoredLine($"QUERY: {query3}", ConsoleColor.Yellow);
AgentResponse result3 = await motorsAgent.RunAsync(query3, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result3}\n", ConsoleColor.Yellow);
ColorHelper.PrintColoredLine("""
  >>> Backward ran the full 10 m — no constraint, no audit. The robot could hit a wall.
  """, ConsoleColor.DarkYellow);

// =============================================================================
// SUMMARY: What Each Middleware Project Adds
// =============================================================================
ColorHelper.PrintColoredLine("""
  --- WHAT EACH MIDDLEWARE PROJECT ADDS ---

  Step | Project                            | Middleware              | Story | Gap in Baseline
  -----|------------------------------------|-------------------------|-------|----------------------------
  1    | ChatClientSharedFunctionMiddleware  | LimitRequests           | —     | Unlimited LLM round-trips
  1    | ChatClientSharedFunctionMiddleware  | RemoveEmail             | S2    | Email leaks to LLM (GDPR)
  2    | ChatClientResponseMiddleware        | EnforceTokenBudget      | S1    | Token spend unchecked ($10,847)
  2    | ChatClientResponseMiddleware        | AddTimestamp            | —     | No response timestamps
  3    | ChatClientFunctionCallingMiddleware | ConstrainDistance       | S3    | Backward unconstrained
  3    | ChatClientFunctionCallingMiddleware | AuditFunctionCalling    | —     | No tool-call audit

  Pipeline when all three are combined:
    .Use(SharedFunction.LimitRequests)        // 1. prepare — fail fast
    .Use(SharedFunction.RemoveEmail)          // 1. prepare — sanitize  (Story 2: GDPR)
    .Use(Response.EnforceTokenBudget, null)   // 2. handle — token budget (Story 1: $10,847 bill)
    .Use(Response.AddTimestamp, null)         // 2. handle — timestamps
    .UseFunctionInvocation(...)               // 3. invoke — always last (Story 3: ConstrainDistance)
  """, ConsoleColor.DarkGray);
