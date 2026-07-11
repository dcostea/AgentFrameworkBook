namespace Middleware;

/// <summary>
/// Thrown by <see cref="AgentSharedFunctions.AgentGuardrails"/> when the operator
/// stored in <c>session.StateBag</c> under the key <c>OperatorName</c> is not
/// authorised to issue commands. Only operators with the role <c>"driver"</c> are
/// permitted to proceed.
/// </summary>
public class OperationDeniedException(string operatorName)
  : Exception($"Operation denied: operator '{operatorName}' is not authorised to issue commands. Only 'driver' role is permitted.")
{ }
