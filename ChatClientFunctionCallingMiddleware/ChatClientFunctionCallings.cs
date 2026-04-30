using Helpers;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Text.Json;

namespace Middleware;

/// <summary>
/// ChatClient FunctionCalling Middleware — Step 3 of 3: Invoke
///
/// Pattern: UseFunctionInvocation with FunctionInvoker delegate
/// Signature: ValueTask&lt;object?&gt;(FunctionInvocationContext, CancellationToken)
///
/// Intercepts every tool invocation. Fires once per tool call — multiple times
/// per GetResponseAsync if the LLM requests several tools in one response.
///
/// Key characteristics:
/// - TERMINAL: no next delegate. Each method must call InvokeAsync directly.
///   Composing two terminal methods that each call InvokeAsync causes double execution.
///   Solution: transform + invoke pattern — ConstrainDistance transforms arguments and returns null (always proceeds),
///   and a single terminal method (AuditFunctionCalls) owns InvokeAsync.
/// - No session context, no agent identity — universal across all agents.
/// - CHAINABLE at the Agent layer (has next); TERMINAL only at this ChatClient layer.
///
/// Recommended position: INNERMOST — .UseFunctionInvocation must be the last
/// middleware registered before .Build().
/// Consequence of placing it outer: its internal tool loop drives repeated calls back
/// through the entire pipeline. SharedFunction and Response fire once per internal
/// LLM round-trip instead of once per external GetResponseAsync call.
///
/// prepare → handle → invoke
///                      ^^^^^^
///                      FunctionCalling is here.
/// </summary>
public static class ChatClientFunctionCallings
{
  /// <summary>
  /// Transform-only: constrains the distance argument to a safe maximum BEFORE the tool is invoked.
  ///
  /// Demonstrates argument transformation: modifies context.Arguments in-place,
  /// then returns null to let AuditFunctionCalls proceed with the constrained value.
  /// The shared context object means AuditFunctionCalls sees the already-constrained argument.
  ///
  /// This method exists because FunctionCalling is TERMINAL — two methods each calling
  /// InvokeAsync would execute the tool twice. The gate pattern solves this by separating
  /// transformation from execution: this method never calls InvokeAsync, and
  /// a single terminal method (AuditFunctionCalls) owns the one InvokeAsync call.
  ///
  /// Story: The Robot Near a Wall
  /// A user asks the robot to reverse 8 meters. There is a wall 5 meters behind it.
  /// This middleware silently constrains backward distance so the robot never hits the wall.
  /// Forward moves are unrestricted.
  /// </summary>
  public static async Task<object?> ConstrainDistance(FunctionInvocationContext context, CancellationToken cancellationToken)
  {
    const int MaxBackwardDistance = 5;
    bool isBackward = context.Function.Name.Contains("backward", StringComparison.OrdinalIgnoreCase);
    bool hasDistance = context.Arguments.TryGetValue("distance", out object? value);
    if (isBackward && hasDistance)
    {
      int distance = value is JsonElement jsonElement
        ? jsonElement.GetInt32()
        : Convert.ToInt32(value);
      if (distance > MaxBackwardDistance)
      {
        context.Arguments["distance"] = JsonSerializer.SerializeToElement(MaxBackwardDistance); // persist as JsonElement for downstream consistency
        ColorHelper.PrintColoredLine($"[ChatClient] [FunctionCall] [Constrain] Backward distance constrained from {distance}m to {MaxBackwardDistance}m", ConsoleColor.Yellow);
      }
    }

    return null;
  }

  /// <summary>
  /// Audits every tool call with timestamp, arguments, timing, and result.
  /// Owns the single InvokeAsync call — the terminal invoker in the gate pattern.
  ///
  /// Fires once per tool call. No agent.Name available: ChatClient does not know
  /// which agent triggered the invocation.
  ///
  /// Contrast with Agent.AuditFunctionCalls: that version is CHAINABLE (has next)
  /// and includes agent.Name. This version is TERMINAL and anonymous.
  /// </summary>
  public static async Task<object?> AuditFunctionCalling(FunctionInvocationContext context, CancellationToken cancellationToken)
  {
    var functionName = context.Function.Name;
    var timestamp = DateTime.UtcNow;
    var stopwatch = Stopwatch.StartNew();

    var result = await context.Function.InvokeAsync(context.Arguments, cancellationToken);
    stopwatch.Stop();
    ColorHelper.PrintColoredLine($"[ChatClient] [FunctionCall] [Audit] Function call '{functionName}' [{timestamp:HH:mm:ss}] COMPLETED in {stopwatch.ElapsedMilliseconds}ms | Result: {result}", ConsoleColor.Green);
    return result;
  }
}
