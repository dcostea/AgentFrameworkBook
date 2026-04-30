using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Text.Json;

namespace Middleware;

/// <summary>
/// Agent FunctionCalling Middleware — Step 3 of 3: Invoke
///
/// Pattern: Use(Func&lt;agent, context, next, ct, ValueTask&lt;object?&gt;&gt;)
///
/// Intercepts every tool invocation. CHAINABLE — each method receives a next
/// delegate and calls it explicitly. The framework invokes the tool exactly once
/// at the end of the next chain. Do NOT call context.Function.InvokeAsync directly:
/// that would execute the tool a second time.
///
/// prepare → handle → invoke
///                      ^^^^^^
///                      FunctionCalling is here.
/// </summary>
public static class AgentFunctionCallings
{
  /// <summary>
  /// Requires human approval for dangerous move sequences (forward → backward without stop).
  /// Includes agent.Name in the approval prompt — impossible at the ChatClient layer.
  ///
  /// Story 3: The Runaway Robot — backward() drove into a wall!
  /// </summary>
  public static async ValueTask<object?> PreventDangerousMoves(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    const string LastMoveKey = "__last_move";
    string functionName = context.Function.Name;

    context.Arguments.TryGetValue(LastMoveKey, out object? lastMoveObj);
    string lastMove = lastMoveObj as string ?? string.Empty;

    bool isDangerous = functionName.Equals("backward", StringComparison.OrdinalIgnoreCase)
                       && lastMove.Equals("forward", StringComparison.OrdinalIgnoreCase);

    if (!isDangerous)
    {
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Safety] SAFE operation '{functionName}' (last: '{lastMove}') - no approval needed", ConsoleColor.Yellow);
      context.Arguments[LastMoveKey] = functionName;
      return await next(context, cancellationToken);
    }

    string args = string.Join(", ", context.Arguments
      .Where(kvp => !kvp.Key.StartsWith("__"))
      .Select(kvp => $"{kvp.Key}={kvp.Value}"));

    ColorHelper.PrintColoredLine($"""
      [Agent] HUMAN APPROVAL REQUIRED
      Agent:     {agent.Name}
      Function:  {functionName}
      Arguments: {args}
      Reason:    Dangerous sequence detected: forward → backward without stop
      """, ConsoleColor.Yellow);

    ColorHelper.PrintColoredLine("WARNING: Robot may back into unseen obstacles (Story 3: The Runaway Robot)", ConsoleColor.DarkYellow);
    ColorHelper.PrintColored("Do you approve this dangerous operation? [Y/n]: ", ConsoleColor.Yellow);

    ConsoleKeyInfo response = Console.ReadKey();
    ColorHelper.PrintColoredLine("\n");

    bool isApproved = response.Key == ConsoleKey.Y || response.Key == ConsoleKey.Enter;

    if (isApproved)
    {
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Safety] APPROVED - Executing {functionName}({args})", ConsoleColor.Yellow);
      context.Arguments[LastMoveKey] = functionName;
      object? result = await next(context, cancellationToken);
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Safety] COMPLETED {functionName} successfully", ConsoleColor.Green);
      return result;
    }
    else
    {
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Safety] DENIED - {functionName} was not approved", ConsoleColor.Red);
      return $"OPERATION DENIED: {functionName} was not approved by human operator.";
    }
  }

  /// <summary>
  /// Clamps backward distance to a safe maximum, reading the limit from
  /// context.Arguments["__session_max_backward"] (falls back to 5m default).
  /// Calls next after transformation — fully chainable.
  ///
  /// Evolved from ChatClient.ConstrainDistance: that version is terminal (no next),
  /// uses a hardcoded limit, and has no agent identity. This version reads the limit
  /// per session, knows agent.Name, and chains via next.
  /// </summary>
  public static async ValueTask<object?> ConstrainDistance(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    const int DefaultMaxBackward = 5;
    bool isBackward = context.Function.Name.Contains("backward", StringComparison.OrdinalIgnoreCase);

    if (isBackward && context.Arguments.TryGetValue("distance", out object? value))
    {
      int maxBackward = DefaultMaxBackward;
      if (context.Arguments.TryGetValue("__session_max_backward", out object? sessionLimit) && sessionLimit is int limit)
      {
        maxBackward = limit;
      }

      int distance = value is JsonElement jsonElement
        ? jsonElement.GetInt32()
        : Convert.ToInt32(value);

      if (distance > maxBackward)
      {
        context.Arguments["distance"] = JsonSerializer.SerializeToElement(maxBackward);
        ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Constrain] Agent '{agent.Name}': backward distance constrained from {distance}m to {maxBackward}m", ConsoleColor.Yellow);
      }
    }

    return await next(context, cancellationToken);
  }

  /// <summary>
  /// Audits every tool call with agent identity, timestamp, arguments, timing, and result.
  /// Chainable — wraps next so timing captures the full downstream execution.
  ///
  /// Evolved from ChatClient.AuditFunctionCalling: that version is terminal and anonymous.
  /// This version is chainable and includes agent.Name.
  /// </summary>
  public static async ValueTask<object?> AuditFunctionCalling(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    string functionName = context.Function.Name;
    string args = string.Join(", ", context.Arguments
      .Where(kvp => !kvp.Key.StartsWith("__"))
      .Select(kvp => $"{kvp.Key}={kvp.Value}"));
    string timestamp = DateTime.UtcNow.ToString("HH:mm:ss");

    ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit] [{timestamp}] Agent '{agent.Name}' invoking '{functionName}({args})'", ConsoleColor.Yellow);

    Stopwatch stopwatch = Stopwatch.StartNew();

    try
    {
      object? result = await next(context, cancellationToken);
      stopwatch.Stop();
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit] [{timestamp}] COMPLETED in {stopwatch.ElapsedMilliseconds}ms | Result: {result}", ConsoleColor.Green);
      return result;
    }
    catch (Exception ex)
    {
      stopwatch.Stop();
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit] [{timestamp}] FAILED in {stopwatch.ElapsedMilliseconds}ms | Error: {ex.Message}", ConsoleColor.Red);
      throw;
    }
  }

  /// <summary>
  /// Tracks per-session tool usage and enforces a budget keyed by tool name.
  /// Short-circuits with a synthetic result if the count exceeds the threshold.
  ///
  /// New: ChatClient has no session, so per-session counters are impossible there.
  /// </summary>
  public static async ValueTask<object?> SessionToolBudget(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    const int MaxCallsPerTool = 10;
    const string UsagePrefix = "__tool_usage_";
    string functionName = context.Function.Name;
    string usageKey = $"{UsagePrefix}{functionName}";

    int currentCount = 0;
    if (context.Arguments.TryGetValue(usageKey, out object? countObj) && countObj is int count)
    {
      currentCount = count;
    }
    currentCount++;
    context.Arguments[usageKey] = currentCount;

    if (currentCount > MaxCallsPerTool)
    {
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Budget] SKIPPED: Agent '{agent.Name}' exceeded budget for '{functionName}' ({currentCount}/{MaxCallsPerTool})", ConsoleColor.Red);
      return $"Tool call skipped: usage limit reached for '{functionName}' in this session ({currentCount}/{MaxCallsPerTool}).";
    }

    ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Budget] Agent '{agent.Name}' tool '{functionName}' usage {currentCount}/{MaxCallsPerTool}", ConsoleColor.Yellow);
    return await next(context, cancellationToken);
  }
}
