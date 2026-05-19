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
  /// <summary>
  /// Blocks direction reversals without an intervening stop command.
  /// Reads the previous tool call from <c>context.Messages</c> — the in-flight chat
  /// history — and refuses to call <c>next</c> when a reversal is detected:
  /// - <c>forward → backward</c>
  /// - <c>backward → forward</c>
  ///
  /// Returns a synthetic blocking result instead of executing the motor command.
  /// The agent sees the refusal and can decide to re-plan with a stop in between.
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

    var stopwatch = Stopwatch.StartNew();

    try
    {
      // CALL NEXT - chainable!
      var result = await next(context, cancellationToken);
      stopwatch.Stop();

      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit] [{timestamp:HH:mm:ss}] COMPLETED in {stopwatch.ElapsedMilliseconds}ms | Result: {result}", ConsoleColor.Green);

      return result;
    }
    catch (Exception ex)
    {
      stopwatch.Stop();
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit] [{timestamp:HH:mm:ss}] FAILED in {stopwatch.ElapsedMilliseconds}ms | Error: {ex.Message}", ConsoleColor.Red);
      throw;
    }
  }
}
