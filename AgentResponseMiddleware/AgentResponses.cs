using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Middleware;

/// <summary>
/// Agent Response Middleware - Request/Response Interception Pattern
/// 
/// Pattern: Use(runFunc: delegate, runStreamingFunc: delegate)
/// Signature: Task&lt;AgentResponse&gt;(messages, session, options, innerAgent, cancellationToken)
/// 
/// This middleware type intercepts the entire request/response cycle.
/// Unlike SharedFunction, you have access to:
/// - The innerAgent (including agent.Name!)
/// - The AgentResponse to inspect/modify
/// 
/// Perfect for: auditing, metrics, response filtering, caching.
///
/// Middleware in this project:
/// - MeasureResponseTime: Performance metrics per agent (existing)
/// - AuditConversation: Full audit with agent identity (existing)
/// - CaptainsLog: Persona-aware journal prefix (HIGH PRIORITY, evolved from ChatClient.AddTimestamp)
/// - PersonaAlignedErrorResponse: Persona-specific friendly error on failure
/// - SessionAwareVerbosityShaper: Adapt response length based on session AnswerMode
/// - RiskSensitiveDowngrader: Override risky answers with escalation
/// - AgentRunAnalyticsEnvelope: Telemetry with agent/session identity
/// - SessionTranscriptScrubberAndExport: Session-wide redacted audit log
/// </summary>
public class AgentResponses
{
  private const string EmailPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
  private const string EmailMask = "[REDACTED-EMAIL]";

  /// <summary>
  /// Measures full agent response time including LLM calls, tool executions, and orchestration.
  /// Demonstrates: Performance metrics with agent identity (innerAgent.Name).
  /// Unlike ChatClient.MeasureResponseTime, this measures the entire agent.RunAsync() execution,
  /// not just the LLM API call.
  /// </summary>
  public static async Task<AgentResponse> MeasureResponseTime(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    var stopwatch = Stopwatch.StartNew();

    ColorHelper.PrintColoredLine($"[Agent] [Response] [Metrics] Starting execution for '{innerAgent.Name}'...", ConsoleColor.Yellow);

    var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    stopwatch.Stop();
    var elapsedMs = stopwatch.ElapsedMilliseconds;

    // Log with severity based on duration
    if (elapsedMs < 1000)
    {
      ColorHelper.PrintColoredLine($"[Agent] [Response] [Metrics] '{innerAgent.Name}' responded in {elapsedMs}ms (fast)", ConsoleColor.Green);
    }
    else if (elapsedMs < 5000)
    {
      ColorHelper.PrintColoredLine($"[Agent] [Response] [Metrics] '{innerAgent.Name}' responded in {elapsedMs}ms (normal)", ConsoleColor.Yellow);
    }
    else
    {
      ColorHelper.PrintColoredLine($"[Agent] [Response] [Metrics] '{innerAgent.Name}' responded in {elapsedMs}ms (SLOW!)", ConsoleColor.Red);
    }

    return response;
  }

  /// <summary>
  /// Audits all agent conversations with timestamps, agent identity, and session tracking.
  /// Demonstrates: Full request/response logging with agent identity (innerAgent.Name).
  /// ChatClient Response middleware has no equivalent audit method; it lacks agent.Name
  /// and session ID, making per-agent conversation logging an Agent-only capability.
  /// </summary>
  public static async Task<AgentResponse> AuditConversation(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    var timestamp = DateTime.UtcNow;
    var sessionId = session?.GetHashCode().ToString() ?? "no-session";
    var userQuery = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "N/A";

    // ═══════════════════════════════════════════════════════════════
    // LOG THE REQUEST (with agent identity!)
    // ═══════════════════════════════════════════════════════════════

    ColorHelper.PrintColoredLine($"""
      [Agent] [Response] [Audit] ---
      [Agent] [Response] [Audit] Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss}
      [Agent] [Response] [Audit] Agent: {innerAgent.Name}
      [Agent] [Response] [Audit] Session: {sessionId}
      [Agent] [Response] [Audit] Query: {userQuery}
      """, ConsoleColor.Yellow);

    // ═══════════════════════════════════════════════════════════════
    // EXECUTE THE AGENT
    // ═══════════════════════════════════════════════════════════════

    var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    // ═══════════════════════════════════════════════════════════════
    // LOG THE RESPONSE
    // ═══════════════════════════════════════════════════════════════

    ColorHelper.PrintColoredLine($"""
      [Agent] [Response] [Audit] Response: {response.Text}
      [Agent] [Response] [Audit] ---
      """, ConsoleColor.Yellow);

    return response;
  }

  /// <summary>
  /// Prepends a "Captain's log" journal entry to every agent response, including
  /// agent name, session id, timestamp, and optional session flags.
  ///
  /// Evolved from <c>ChatClientResponses.AddTimestamp</c>: that version stamps with a
  /// bare UTC datetime. This version produces a persona-aware journal entry tied to
  /// an agent and a session — a true "board journal entry" for that specific agent instance.
  ///
  /// Why Agent layer: can include agent.Name, session id, tenant, environment, and
  /// per-session flags in the prefix. ChatClient only has an anonymous transport-level stamp.
  /// </summary>
  public static async Task<AgentResponse> CaptainsLog(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    string timestamp = DateTimeOffset.UtcNow.ToString("o");
    string sessionId = session?.GetHashCode().ToString("X8") ?? "no-session";

    // Collect optional session context for richer journal entries
    string? environment = null;
    session?.StateBag.TryGetValue<string>("Environment", out environment);
    string envTag = !string.IsNullOrWhiteSpace(environment) ? $" Env {environment}." : string.Empty;

    string journalPrefix = $"Captain's log. Stardate {timestamp}. Agent {innerAgent.Name}. Session {sessionId}.{envTag} ";

    ColorHelper.PrintColoredLine($"[Agent] [Response] [CaptainsLog] {journalPrefix}", ConsoleColor.Cyan);

    // Prepend the journal prefix to the last assistant message
    foreach (ChatMessage message in response.Messages)
    {
      if (message.Role == ChatRole.Assistant && !string.IsNullOrEmpty(message.Text))
      {
        message.Contents = [new TextContent($"{journalPrefix}{message.Text}")];
      }
    }

    return response;
  }

  /// <summary>
  /// Wraps agent execution in a try/catch and produces a persona-aligned friendly error
  /// response when the agent fails. The error tone depends on <c>innerAgent.Name</c>:
  /// for example, "MotorsAgent" says it "stopped the car for safety".
  ///
  /// Evolved from the ChatClient token-guardrail concept (synthetic response instead of
  /// calling the LLM), but now at the agent and persona level with full session context.
  ///
  /// Why Agent layer: persona-specific error text requires agent.Name; session context
  /// lets us mark <c>session.StateBag["LastRunFailed"]</c> for downstream logic.
  /// </summary>
  public static async Task<AgentResponse> PersonaAlignedErrorResponse(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    try
    {
      AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
      session?.StateBag.SetValue("LastRunFailed", (object)false);
      return response;
    }
    catch (OperationCanceledException)
    {
      throw; // let cancellation bubble up
    }
    catch (Exception ex)
    {
      session?.StateBag.SetValue("LastRunFailed", (object)true);

      string agentName = innerAgent.Name ?? "Agent";
      string friendlyMessage = agentName switch
      {
        "MotorsAgent" => "I've stopped the car for safety. Something went wrong with my navigation system. Please try again or take manual control.",
        "SupportAgent" => "I'm sorry, I ran into a problem while looking up your request. Let me connect you with a human specialist.",
        _ => $"I encountered an unexpected issue and cannot complete your request right now. Please try again."
      };

      ColorHelper.PrintColoredLine($"[Agent] [Response] [ErrorHandler] Agent '{agentName}' failed: {ex.Message}", ConsoleColor.Red);
      ColorHelper.PrintColoredLine($"[Agent] [Response] [ErrorHandler] Returning persona-aligned error: '{friendlyMessage}'", ConsoleColor.DarkYellow);

      return new AgentResponse(new ChatMessage(ChatRole.Assistant, friendlyMessage));
    }
  }

  /// <summary>
  /// Scans the candidate response plus session history for risky patterns (e.g.,
  /// repeated questions about controlling hardware or transferring funds). If the
  /// risk score exceeds a threshold, discards the response and returns an escalation
  /// message. Stamps <c>session.StateBag["EscalationRequired"]</c> for downstream logic.
  ///
  /// New: risk assessment is per-agent and per-session. ChatClient cannot track
  /// "repeated risky intent" across a session.
  /// </summary>
  public static async Task<AgentResponse> RiskSensitiveDowngrader(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    // Simple risk heuristic: count risky keywords across all user messages
    string[] riskyKeywords = ["transfer funds", "wire money", "shutdown", "override safety", "disable alarm", "emergency override"];
    int riskScore = 0;
    foreach (ChatMessage message in messages.Where(m => m.Role == ChatRole.User && m.Text is not null))
    {
      foreach (string keyword in riskyKeywords)
      {
        if (message.Text!.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
          riskScore++;
        }
      }
    }

    const int RiskThreshold = 2;

    if (riskScore >= RiskThreshold)
    {
      session?.StateBag.SetValue("EscalationRequired", (object)true);

      string escalationMessage = $"I've detected repeated high-risk requests in this session (risk score: {riskScore}). "
        + "For safety, I must escalate this conversation to a human operator. "
        + "Please wait for a supervisor to review and approve further actions.";

      ColorHelper.PrintColoredLine($"[Agent] [Response] [Risk] Agent '{innerAgent.Name}' risk score {riskScore} >= {RiskThreshold} — ESCALATING", ConsoleColor.Red);

      return new AgentResponse(new ChatMessage(ChatRole.Assistant, escalationMessage));
    }

    ColorHelper.PrintColoredLine($"[Agent] [Response] [Risk] Agent '{innerAgent.Name}' risk score {riskScore} — within safe range", ConsoleColor.Green);
    return response;
  }

  /// <summary>
  /// Measures wall-clock latency for the entire agent run (including tools) and emits a
  /// telemetry record with AgentName, SessionId, UserId, success status, and answer length.
  /// Returns the original response unchanged.
  ///
  /// Evolved from <c>ChatClientResponses.AddTimestamp</c> and the ChatClient metrics pattern,
  /// but now on <c>AgentResponse</c> with full session context. This is the view product teams
  /// care about ("one agent run"), not the LLM round-trip view.
  /// </summary>
  public static async Task<AgentResponse> AgentRunAnalyticsEnvelope(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    string sessionId = session?.GetHashCode().ToString("X8") ?? "no-session";
    string? userId = null;
    session?.StateBag.TryGetValue<string>("UserId", out userId);
    string? correlationId = null;
    session?.StateBag.TryGetValue<string>("CorrelationId", out correlationId);
    userId ??= "anonymous";
    correlationId ??= Guid.NewGuid().ToString("N")[..8];

    var stopwatch = Stopwatch.StartNew();
    bool success = true;
    AgentResponse response;

    try
    {
      response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
    }
    catch (Exception)
    {
      success = false;
      stopwatch.Stop();
      ColorHelper.PrintColoredLine($"[Agent] [Response] [Analytics] FAILED | Agent={innerAgent.Name} Session={sessionId} User={userId} Corr={correlationId} Duration={stopwatch.ElapsedMilliseconds}ms", ConsoleColor.Red);
      throw;
    }

    stopwatch.Stop();
    int answerLength = response.Text?.Length ?? 0;
    bool usedTools = response.Messages.Any(m => m.Contents.OfType<FunctionCallContent>().Any());

    ColorHelper.PrintColoredLine($"""
      [Agent] [Response] [Analytics] ---
      [Agent] [Response] [Analytics] Agent:       {innerAgent.Name}
      [Agent] [Response] [Analytics] Session:     {sessionId}
      [Agent] [Response] [Analytics] User:        {userId}
      [Agent] [Response] [Analytics] Correlation: {correlationId}
      [Agent] [Response] [Analytics] Duration:    {stopwatch.ElapsedMilliseconds}ms
      [Agent] [Response] [Analytics] Success:     {success}
      [Agent] [Response] [Analytics] UsedTools:   {usedTools}
      [Agent] [Response] [Analytics] AnswerLen:   {answerLength} chars
      [Agent] [Response] [Analytics] ---
      """, ConsoleColor.Cyan);

    return response;
  }
}
