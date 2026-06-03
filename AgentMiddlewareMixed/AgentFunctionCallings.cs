using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Text.Json;

namespace Middleware;

/// <summary>
/// Agent FunctionCalling Middleware - Tool Invocation Control Pattern
/// 
/// Pattern: Use(Func&lt;agent, context, next, ct, ValueTask&lt;object?&gt;&gt;)
/// 
/// This middleware type intercepts function/tool invocations.
/// CRITICAL: Agent FunctionCalling middleware is CHAINABLE (has `next` delegate)!
/// 
/// Key Features:
/// - Access to agent identity (agent.Name)
/// - Access to function context (name, arguments)
/// - Has `next` delegate for chaining multiple middlewares
/// - Perfect for: human approval, auditing, parameter validation
/// 
/// Middleware in this project:
///   - PreventDangerousMoves: Blocks forward→backward and backward→forward reversals
///   - AuditAgentFunctionCalls: Logging with timing and agent identity
/// </summary>
public static class AgentFunctionCallings
{
  private const string MotorsAgentName = "MotorsAgent";

  /// <summary>
  /// Blocks direction reversals without an intervening stop command, but only for
  /// <c>MotorsAgent</c>. Other agents pass through unconditionally.
  /// Reads the previous tool call from <c>context.Messages</c> — the in-flight chat
  /// history — and refuses to call <c>next</c> when a reversal is detected:
  /// - <c>forward → backward</c>
  /// - <c>backward → forward</c>
  ///
  /// Returns a synthetic blocking result instead of executing the motor command.
  /// The agent sees the refusal and can decide to re-plan with a stop in between.
  ///
  /// Why Agent layer: <c>agent.Name</c> is available here, making it possible to
  /// scope this safety rule to a specific agent.
  ///
  /// To audit the full sequence after the run completes, use Response middleware
  /// (<c>MovementSequenceAuditor</c>), which sees all tool calls at once.
  /// </summary>
  public static async ValueTask<object?> PreventDangerousMoves(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    if (!string.Equals(agent.Name, MotorsAgentName))
    {
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Safety] SKIP: rule does not apply to agent '{agent.Name}'", ConsoleColor.Yellow);
      return await next(context, cancellationToken);
    }

    string current = context.Function.Name;

    string previous = context.Messages
      .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
      .LastOrDefault()?.Name ?? string.Empty;

    bool isDangerous = $"{previous}-{current}" is "forward-backward" or "backward-forward";

    if (isDangerous)
    {
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Safety] BLOCKED: '{previous}-{current}' is an illegal reversal — a 'stop' is required first.", ConsoleColor.Red);
      return $"BLOCKED: cannot execute '{current}' directly after '{previous}'. Issue a stop command first.";
    }

    ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Safety] SAFE: '{current}' (last: '{previous}')", ConsoleColor.Yellow);
    return await next(context, cancellationToken);
  }

  /// <summary>
  /// Audits all function calls with timing and results, including agent identity.
  /// Demonstrates: Function call logging with agent identity (agent.Name).
  /// Unlike ChatClient.AuditFunctionCalls, this is CHAINABLE (has next delegate)
  /// and includes agent.Name in the audit log.
  /// </summary>
  public static async ValueTask<object?> AuditAgentFunctionCalls(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    var functionName = context.Function.Name;
    var args = string.Join(", ", context.Arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    var timestamp = DateTime.UtcNow;

    ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit] [{timestamp:HH:mm:ss}] Agent '{agent.Name}' invoking '{functionName}({args})'", ConsoleColor.Yellow);

    try
    {
      var result = await next(context, cancellationToken);
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit] [{timestamp:HH:mm:ss}] COMPLETED | Result: {result}", ConsoleColor.Green);

      return result;
    }
    catch (Exception ex)
    {
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit] [{timestamp:HH:mm:ss}] FAILED | Error: {ex.Message}", ConsoleColor.Red);
      throw;
    }
  }
}
