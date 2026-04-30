using Helpers;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Text.Json;

namespace Middleware;

public static class ChatClientFunctionCallings
{
  public static async Task<object?> ConstrainDistance(
    FunctionInvocationContext context, 
    CancellationToken cancellationToken)
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
        // persist as JsonElement for downstream consistency
        context.Arguments["distance"] = JsonSerializer.SerializeToElement(MaxBackwardDistance);
        ColorHelper.PrintColoredLine($"[ChatClient] [FunctionCall] [Constrain] " +
          $"Backward distance constrained from {distance}m to {MaxBackwardDistance}m", ConsoleColor.Red);
      }
    }

    return null;
  }

  public static async Task<object?> AuditFunctionCalling(
    FunctionInvocationContext context, 
    CancellationToken cancellationToken)
  {
    var functionName = context.Function.Name;
    var timestamp = DateTime.UtcNow;
    var stopwatch = Stopwatch.StartNew();

    var result = await context.Function.InvokeAsync(context.Arguments, cancellationToken);
    stopwatch.Stop();
    ColorHelper.PrintColoredLine($"[ChatClient] [FunctionCall] [Audit] " +
      $"Function call '{functionName}' [{timestamp:HH:mm:ss}] COMPLETED in " +
      $"{stopwatch.ElapsedMilliseconds}ms | Result: {result}", ConsoleColor.Yellow);

    return result;
  }
}
