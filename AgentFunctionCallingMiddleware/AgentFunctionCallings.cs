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
/// Story 3: The Runaway Robot - prevents dangerous operations without approval.
///
/// Middleware in this project:
/// Existing:
///   - PreventDangerousMoves: Human-in-the-loop approval (existing)
///   - AuditFunctionCalls: Logging with timing (existing)
/// New:
///   - AgentConstrainDistanceGate: Evolved constrain with session-aware max (HIGH PRIORITY)
///   - AgentAuditFunctionCalling: Evolved audit with session/tenant tags (HIGH PRIORITY)
///   - AgentToolAllowDenyList: Per-agent tool restrictions
///   - SessionToolBudget: Per-session tool usage counter
///   - ContextAwareToolArgumentFiller: Inject missing args from session
///   - HumanApprovalGateWithChainableAudit: Combined gate + audit pattern
///   - ToolResultPostProcessorWithSessionMemory: Normalize/store tool results
/// </summary>
public static class AgentFunctionCallings
{
  /// <summary>
  /// Requires human approval for dangerous operations (backward, stop).
  /// Demonstrates: Human-in-the-loop pattern with agent identity (agent.Name in approval UI).
  /// Unlike ChatClient.PreventDangerousMoves (gate-only), this is CHAINABLE
  /// (has next delegate) and includes agent.Name in the approval prompt.
  /// 
  /// Story 3: The Runaway Robot - backward() drove into a wall!
  /// </summary>
  public static async ValueTask<object?> PreventDangerousMoves(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    const string LastMoveKey = "__last_move";
    string functionName = context.Function.Name;

    // Read the last tracked move from session arguments
    context.Arguments.TryGetValue(LastMoveKey, out object? lastMoveObj);
    string lastMove = lastMoveObj as string ?? string.Empty;

    // Dangerous sequence: forward → backward without a stop in between
    bool isDangerous = functionName.Equals("backward", StringComparison.OrdinalIgnoreCase)
                       && lastMove.Equals("forward", StringComparison.OrdinalIgnoreCase);

    if (!isDangerous)
    {
      // Safe operation - update move tracking and proceed
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Safety] SAFE operation '{functionName}' (last: '{lastMove}') - no approval needed", ConsoleColor.Yellow);
      context.Arguments[LastMoveKey] = functionName;
      return await next(context, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════
    // DANGEROUS SEQUENCE - REQUIRE HUMAN APPROVAL
    // ═══════════════════════════════════════════════════════════════

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

      // CALL NEXT - this is what makes Agent FunctionCalling CHAINABLE!
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
  /// Audits all function calls with timing and results, including agent identity.
  /// Demonstrates: Function call logging with agent identity (agent.Name).
  /// Unlike ChatClient.AuditFunctionCalls, this is CHAINABLE (has next delegate)
  /// and includes agent.Name in the audit log.
  /// </summary>
  public static async ValueTask<object?> AuditFunctionCalls(
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

  // ═══════════════════════════════════════════════════════════════════════════
  // NEW: Evolved and session-aware FunctionCalling middleware
  // ═══════════════════════════════════════════════════════════════════════════

  /// <summary>
  /// Clamps backward distance to a safe maximum, reading the limit from
  /// <c>session.StateBag["MaxBackwardDistance"]</c> instead of a hardcoded value.
  /// Calls <c>next</c> after transformation — fully chainable.
  ///
  /// Evolved from <c>ChatClientFunctionCallings.ConstrainDistance</c>: that version is
  /// terminal (no next), uses a hardcoded 5m limit, and has no agent or session identity.
  /// This version reads the limit per session, knows agent.Name, and chains via next.
  ///
  /// "We started with anonymous transport-level middleware at ChatClient.
  ///  Once we move the same idea to the Agent layer, we unlock identity."
  /// </summary>
  public static async ValueTask<object?> AgentConstrainDistanceGate(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    const int DefaultMaxBackward = 5;
    string functionName = context.Function.Name;
    bool isBackward = functionName.Contains("backward", StringComparison.OrdinalIgnoreCase);

    if (isBackward && context.Arguments.TryGetValue("distance", out object? value))
    {
      // Read session-aware limit (falls back to default)
      int maxBackward = DefaultMaxBackward;
      if (context.Arguments.TryGetValue("__session_max_backward", out object? sessionLimit)
          && sessionLimit is int limit)
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

    // Chainable: call next (unlike ChatClient's terminal pattern)
    return await next(context, cancellationToken);
  }

  /// <summary>
  /// Richer audit with session id, agent name, and tenant/region tags.
  /// Evolved from <c>ChatClientFunctionCallings.AuditFunctionCalling</c>: that version is
  /// terminal and anonymous. This version is chainable and includes full context.
  ///
  /// "Same transform + audit pattern, but now we know which agent, which session,
  ///  which tenant, and we can change behavior accordingly."
  /// </summary>
  public static async ValueTask<object?> AgentAuditFunctionCalling(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    string functionName = context.Function.Name;
    string args = string.Join(", ", context.Arguments
      .Where(kvp => !kvp.Key.StartsWith("__")) // skip internal keys
      .Select(kvp => $"{kvp.Key}={kvp.Value}"));
    string timestamp = DateTime.UtcNow.ToString("HH:mm:ss");

    ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit+] [{timestamp}] Agent '{agent.Name}' invoking '{functionName}({args})'", ConsoleColor.Yellow);

    Stopwatch stopwatch = Stopwatch.StartNew();

    try
    {
      object? result = await next(context, cancellationToken);
      stopwatch.Stop();

      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit+] [{timestamp}] COMPLETED in {stopwatch.ElapsedMilliseconds}ms | Result: {result}", ConsoleColor.Green);
      return result;
    }
    catch (Exception ex)
    {
      stopwatch.Stop();
      ColorHelper.PrintColoredLine($"[Agent] [FunctionCall] [Audit+] [{timestamp}] FAILED in {stopwatch.ElapsedMilliseconds}ms | Error: {ex.Message}", ConsoleColor.Red);
      throw;
    }
  }

  /// <summary>
  /// Tracks per-session tool usage and enforces a budget. Reads and increments
  /// a usage counter keyed by tool name. If the count crosses a threshold,
  /// short-circuits with a synthetic result instead of calling <c>next</c>.
  ///
  /// New: ChatClient has no place to store per-session counters. Agent middleware
  /// uses <c>context.Arguments</c> to carry session state injected by upstream logic.
  /// </summary>
  public static async ValueTask<object?> SessionToolBudget(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    const int MaxCallsPerTool = 10; // per session, per tool
    const string UsagePrefix = "__tool_usage_";
    string functionName = context.Function.Name;
    string usageKey = $"{UsagePrefix}{functionName}";

    // Read current count from arguments (injected by upstream session-aware logic)
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
