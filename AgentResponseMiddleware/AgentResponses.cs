using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

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
/// </summary>
public class AgentResponses
{
  /// <summary>
  /// Prepends a "Captain's log" journal entry to every agent response, including
  /// agent name, session id, timestamp, and optional session flags.
  ///
  /// Evolved from <c>ChatClientResponses.AddTimestamp</c>: that version stamps with a
  /// bare UTC datetime. This version produces a persona-aware journal entry tied to
  /// an agent and a session — a true "board journal entry" for that specific agent instance.
  ///
  /// Why Agent layer: can include agent.Name, session id, and per-session StateBag values
  /// in the prefix. ChatClient only has an anonymous transport-level stamp.
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

    int toolCallCount = response.Messages.Sum(m => m.Contents.OfType<FunctionCallContent>().Count());
    string toolTag = toolCallCount > 0 ? $" Tools fired: {toolCallCount}." : string.Empty;

    string journalPrefix = $"Captain's log. Stardate {timestamp}. Agent {innerAgent.Name}. Session {sessionId}.{envTag}{toolTag} ";

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
  /// Audits the executed movement sequence for illegal direction reversals.
  /// When a violation is detected, appends a warning footer to the assistant message in the response.
  ///
  /// Detected rule: <c>forward → backward</c> and <c>backward → forward</c> without
  /// a <c>stop</c> in between. Does not block — alerting only.
  ///
  /// To block the reversal at runtime, use FunctionCalling middleware (<c>PreventDangerousMoves</c>),
  /// which fires before each tool executes and can prevent the call from reaching the motor.
  ///
  /// Why Response middleware: the complete sequence only exists after <c>innerAgent.RunAsync</c>
  /// returns — SharedFunction has no response yet, FunctionCalling sees one tool at a time.
  /// </summary>
  public static async Task<AgentResponse> MovementSequenceAuditor(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
  {
    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    string sequence = string.Join("-", response.Messages
      .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
      .Select(f => f.Name.ToLowerInvariant()));

    bool isIllegal = sequence.Contains("forward-backward")
      || sequence.Contains("backward-forward");

    if (isIllegal)
    {
      ColorHelper.PrintColoredLine($"[Agent] [Response] [SequenceAuditor] ILLEGAL sequence detected — stop required between direction reversals", ConsoleColor.Red);

      ChatMessage? assistantMessage = response.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant);
      assistantMessage?.Contents = [new TextContent(
        $"{assistantMessage.Text}\n\n⚠ WARNING: illegal direction reversal detected in executed sequence. A stop command was missing.")];
    }
    else
    {
      ColorHelper.PrintColoredLine($"[Agent] [Response] [SequenceAuditor] Sequence is valid.", ConsoleColor.Green);
    }

    return response;
  }
}

