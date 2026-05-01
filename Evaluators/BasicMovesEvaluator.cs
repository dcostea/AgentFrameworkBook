using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using System.Text.RegularExpressions;

namespace Evaluators;

/// <summary>
/// A non-AI-based evaluator that checks if the response contains only valid basic moves
/// for Robby the car: forward, backward, turn left, turn right, stop.
/// Invalid moves like jump, roll, switch, slide will cause the evaluation to fail.
/// </summary>
/// <remarks>
/// The result is returned via a <see cref="BooleanMetric"/> as part of the returned
/// <see cref="EvaluationResult"/>.
/// </remarks>
public class BasicMovesEvaluator : IEvaluator
{
  public const string BasicMovesMetricName = "BasicMoves";

  public IReadOnlyCollection<string> EvaluationMetricNames => [BasicMovesMetricName];

  /// <summary>
  /// Valid basic moves for Robby the car.
  /// </summary>
  private static readonly HashSet<string> ValidMoves = new(StringComparer.OrdinalIgnoreCase)
    {
        "forward", "backward", "turn left", "turn right", "stop",
        "turn_left", "turn_right", "move forward", "move backward",
        "go forward", "go backward", "rotate left", "rotate right",
        "left", "right"
    };

  /// <summary>
  /// Invalid moves that should not appear in Robby's responses.
  /// </summary>
  private static readonly HashSet<string> InvalidMoves = new(StringComparer.OrdinalIgnoreCase)
    {
        "jump", "roll", "switch", "slide", "fly", "teleport", "leap",
        "crawl", "climb", "dive", "sprint", "dash", "bounce", "hover",
        "float", "swim", "skip"
    };

  /// <summary>
  /// Evaluates if the response contains only valid basic moves and no invalid moves.
  /// </summary>
  private static (bool isValid, string reason, List<string> foundValidMoves, List<string> foundInvalidMoves)
      EvaluateBasicMoves(string? input)
  {
    if (string.IsNullOrWhiteSpace(input))
    {
      return (false, "Response is empty or null.", new List<string>(), new List<string>());
    }

    var foundValidMoves = new List<string>();
    var foundInvalidMoves = new List<string>();

    // Convert to lowercase for easier matching
    string lowerInput = input.ToLower();

    // Check for valid moves
    foreach (var validMove in ValidMoves)
    {
      if (lowerInput.Contains(validMove, StringComparison.CurrentCultureIgnoreCase))
      {
        foundValidMoves.Add(validMove);
      }
    }

    // Check for invalid moves
    foreach (var invalidMove in InvalidMoves)
    {
      if (Regex.IsMatch(lowerInput, $@"\b{Regex.Escape(invalidMove.ToLower())}\b"))
      {
        foundInvalidMoves.Add(invalidMove);
      }
    }

    // Determine if valid
    bool hasValidMoves = foundValidMoves.Count > 0;
    bool hasInvalidMoves = foundInvalidMoves.Count > 0;

    string reason;
    bool isValid;

    if (hasInvalidMoves)
    {
      reason = $"""
                Response contains invalid moves: {string.Join(", ", foundInvalidMoves.Distinct())}.
                Only basic moves are allowed: forward, backward, turn left, turn right, stop.
                """;
      isValid = false;
    }
    else if (hasValidMoves)
    {
      reason = $"""
                Response contains only valid basic moves: {string.Join(", ", foundValidMoves.Distinct())}.
                """;
      isValid = true;
    }
    else
    {
      reason = "Response does not contain any recognizable basic moves.";
      isValid = false;
    }

    return (isValid, reason, foundValidMoves, foundInvalidMoves);
  }

  /// Provides interpretation for the supplied <paramref name="metric"/>.
  private static void Interpret(BooleanMetric metric, string reason)
  {
    if (metric.Value is null)
    {
      metric.Interpretation =
          new EvaluationMetricInterpretation(
              EvaluationRating.Unknown,
              failed: true,
              reason: "Failed to evaluate basic moves in the response.");
    }
    else if (metric.Value == true)
    {
      metric.Interpretation =
          new EvaluationMetricInterpretation(
              EvaluationRating.Good,
              reason: reason);
    }
    else
    {
      metric.Interpretation =
          new EvaluationMetricInterpretation(
              EvaluationRating.Unacceptable,
              failed: true,
              reason: reason);
    }
  }

  public ValueTask<EvaluationResult> EvaluateAsync(
      IEnumerable<ChatMessage> messages,
      ChatResponse modelResponse,
      ChatConfiguration? chatConfiguration = null,
      IEnumerable<EvaluationContext>? additionalContext = null,
      CancellationToken cancellationToken = default)
  {
    // Evaluate the basic moves in the response
    var (isValid, reason, foundValidMoves, foundInvalidMoves) = EvaluateBasicMoves(modelResponse.Text);

    // Create a detailed reason including what was found
    string detailedReason = reason;
    if (foundValidMoves.Count > 0)
    {
      detailedReason += $" Valid moves found: {foundValidMoves.Count}.";
    }
    if (foundInvalidMoves.Count > 0)
    {
      detailedReason += $" Invalid moves found: {foundInvalidMoves.Count}.";
    }

    // Create a BooleanMetric with the evaluation result
    var metric = new BooleanMetric(BasicMovesMetricName, value: isValid, detailedReason);

    // Attach interpretation
    Interpret(metric, detailedReason);

    // Return the evaluation result
    return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
  }
}
