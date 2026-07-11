using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Observations;

public class DirectionChangeTracker(ActivitySource activitySource, Counter<int> directionChangeCounter)
{
  private string? _previousLinearDirection;

  public async ValueTask<object?> TrackDirectionChangeAsync(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
  {
    object? result = await next(context, cancellationToken);

    string? currentLinearDirection = context.Function.Name;

    if (!currentLinearDirection.Equals("Forward") && !currentLinearDirection.Equals("Backward"))
    {
      _previousLinearDirection = null; // Reset direction tracking if the command is not linear movement
      return result;
    }

    bool directionChanged = _previousLinearDirection is not null && _previousLinearDirection != currentLinearDirection;

    using Activity? directionActivity = activitySource.StartActivity("Detect Robot Direction Change", ActivityKind.Internal);
    directionActivity?.SetTag("agent.name", agent.Name);
    directionActivity?.SetTag("robot.tool.name", context.Function.Name);
    directionActivity?.SetTag("robot.direction.previous", _previousLinearDirection ?? "none");
    directionActivity?.SetTag("robot.direction.current", currentLinearDirection);
    directionActivity?.SetTag("robot.direction.changed", directionChanged);

    if (directionChanged)
    {
      directionChangeCounter.Add(1, new KeyValuePair<string, object?>("from", _previousLinearDirection), new KeyValuePair<string, object?>("to", currentLinearDirection));
      directionActivity?.AddEvent(new ActivityEvent("Robot direction changed"));
    }

    _previousLinearDirection = currentLinearDirection;

    return result;
  }
}
