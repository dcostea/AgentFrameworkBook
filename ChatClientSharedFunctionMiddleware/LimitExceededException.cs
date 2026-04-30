namespace Middleware;

/// <summary>
/// Thrown by <see cref="ChatClientSharedFunctions.LimitRequests"/> when the
/// configured request limit is exhausted. Catch this at each call site to
/// degrade gracefully instead of crashing the process.
/// </summary>
public class LimitExceededException(int currentCount, int maxRequests)
  : Exception($"Request limit exceeded: request {currentCount} of {maxRequests} blocked!")
{ }
