using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Middleware;

/// <summary>
/// Agent Response Middleware — Step 2 of 3: Handle
///
/// Pattern: Use(runFunc: Func&lt;messages, session, options, innerAgent, ct, Task&lt;AgentResponse&gt;&gt;)
///
/// Intercepts the entire agent request/response cycle. Unlike SharedFunction,
/// has access to innerAgent (including agent.Name!) and the AgentResponse.
///
/// prepare → handle → invoke
///            ^^^^^^
///            Response is here.
/// </summary>
public static class AgentResponses
{
  private const string EmailPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
  private const string EmailMask = "[REDACTED-EMAIL]";

  /// <summary>
  /// Measures full agent response time including LLM calls, tool executions, and orchestration.
  /// Unlike ChatClient.MeasureResponseTime, measures the entire agent.RunAsync() execution.
  /// </summary>
  public static async Task<AgentResponse> MeasureResponseTime(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    ColorHelper.PrintColoredLine($"[Agent] [Response] [Metrics] Starting execution for '{innerAgent.Name}'...", ConsoleColor.Yellow);

    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    stopwatch.Stop();
    long elapsedMs = stopwatch.ElapsedMilliseconds;

    ConsoleColor color = elapsedMs < 1000 ? ConsoleColor.Green
                       : elapsedMs < 5000 ? ConsoleColor.Yellow
                       : ConsoleColor.Red;
    string label = elapsedMs < 1000 ? "fast" : elapsedMs < 5000 ? "normal" : "SLOW!";
    ColorHelper.PrintColoredLine($"[Agent] [Response] [Metrics] '{innerAgent.Name}' responded in {elapsedMs}ms ({label})", color);

    return response;
  }

  /// <summary>
  /// Audits all agent conversations with timestamps, agent identity, and session tracking.
  /// Requires innerAgent.Name — impossible at the ChatClient layer.
  /// </summary>
  public static async Task<AgentResponse> AuditConversation(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    DateTime timestamp = DateTime.UtcNow;
    string sessionId = session?.GetHashCode().ToString() ?? "no-session";
    string userQuery = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "N/A";

    ColorHelper.PrintColoredLine($"""
      [Agent] [Response] [Audit] ---
      [Agent] [Response] [Audit] Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss}
      [Agent] [Response] [Audit] Agent: {innerAgent.Name}
      [Agent] [Response] [Audit] Session: {sessionId}
      [Agent] [Response] [Audit] Query: {userQuery}
      """, ConsoleColor.Yellow);

    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    ColorHelper.PrintColoredLine($"""
      [Agent] [Response] [Audit] Response: {response.Text}
      [Agent] [Response] [Audit] ---
      """, ConsoleColor.Yellow);

    return response;
  }

  /// <summary>
  /// Prepends a persona-aware "Captain's log" journal entry to every agent response.
  ///
  /// Evolved from ChatClient.AddTimestamp: that version stamps with a bare UTC datetime.
  /// This version produces a journal entry tied to an agent, session, and optional
  /// environment tag — information that is unavailable at the ChatClient layer.
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
    string? environment = null;
    session?.StateBag.TryGetValue<string>("Environment", out environment);
    string envTag = !string.IsNullOrWhiteSpace(environment) ? $" Env {environment}." : string.Empty;
    string journalPrefix = $"Captain's log. Stardate {timestamp}. Agent {innerAgent.Name}. Session {sessionId}.{envTag} ";

    ColorHelper.PrintColoredLine($"[Agent] [Response] [CaptainsLog] {journalPrefix}", ConsoleColor.Cyan);

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
  /// response when the agent fails. Error tone depends on innerAgent.Name.
  ///
  /// Marks session.StateBag["LastRunFailed"] for downstream logic.
  /// Impossible at the ChatClient layer — no agent identity and no session.
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
      throw;
    }
    catch (Exception ex)
    {
      session?.StateBag.SetValue("LastRunFailed", (object)true);

      string agentName = innerAgent.Name ?? "Agent";
      string friendlyMessage = agentName switch
      {
        "MotorsAgent" => "I've stopped the car for safety. Something went wrong with my navigation system. Please try again or take manual control.",
        "SupportAgent" => "I'm sorry, I ran into a problem while looking up your request. Let me connect you with a human specialist.",
        _ => "I encountered an unexpected issue and cannot complete your request right now. Please try again."
      };

      ColorHelper.PrintColoredLine($"[Agent] [Response] [ErrorHandler] Agent '{agentName}' failed: {ex.Message}", ConsoleColor.Red);
      ColorHelper.PrintColoredLine($"[Agent] [Response] [ErrorHandler] Returning persona-aligned error: '{friendlyMessage}'", ConsoleColor.DarkYellow);

      return new AgentResponse(new ChatMessage(ChatRole.Assistant, friendlyMessage));
    }
  }

  /// <summary>
  /// Emits a telemetry record with AgentName, SessionId, UserId, duration, success status,
  /// and whether tools were used. Returns the original response unchanged.
  ///
  /// Evolved from the ChatClient metrics pattern, but scoped to a full agent run
  /// (including tools) rather than a single LLM round-trip.
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
    userId ??= "anonymous";
    string correlationId = Guid.NewGuid().ToString("N")[..8];

    Stopwatch stopwatch = Stopwatch.StartNew();
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
