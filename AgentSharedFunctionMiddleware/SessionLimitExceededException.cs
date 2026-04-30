namespace Middleware;

/// <summary>
/// Thrown by <see cref="AgentSharedFunctions.SessionScopedLimitRequests"/> when the
/// per-session run limit is exhausted. Catch this at each call site to degrade
/// gracefully instead of crashing the process.
///
/// Evolved from <see cref="LimitExceededException"/>: that one is app-instance-wide;
/// this one is per-session and per-user.
/// </summary>
public class SessionLimitExceededException(int currentCount, int maxRuns)
  : Exception($"Session run limit exceeded: run {currentCount} of {maxRuns} blocked!")
{ }
