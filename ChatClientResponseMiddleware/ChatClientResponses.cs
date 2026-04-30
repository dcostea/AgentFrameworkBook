using Helpers;
using Microsoft.Extensions.AI;

namespace Middleware;

/// <summary>
/// ChatClient Response Middleware — Step 2 of 3: Handle
///
/// Pattern: Use(Func&lt;messages, options, innerClient, ct, Task&lt;ChatResponse&gt;&gt;, null)
///
/// Intercepts the LLM request/response cycle. Fires once per GetResponseAsync call,
/// wrapping the full LLM exchange from entry to exit.
///
/// Key characteristics:
/// - Fires once per GetResponseAsync call — entry and exit are a single activation.
/// - Sees ChatResponse exclusively: response.Usage (token counts) is available here
///   and nowhere else. Agent Response middleware returns AgentResponse, which does
///   not expose token counts. This makes EnforceTokenBudget a ChatClient-only capability.
/// - No session context, no agent identity (no innerAgent.Name).
///
/// Recommended position: AFTER SharedFunction, BEFORE FunctionCalling.
/// Reason: receives already-sanitized messages from SharedFunction; measures only
/// LLM work (SharedFunction preprocessing time excluded from the window).
///
/// Registration order within Response middleware matters:
///   .Use(EnforceTokenBudget, null)   // outer — pre-flight budget guard
///   .Use(AddTimestamp, null)      // inner — stamps every response with a datetime
///
/// prepare → handle → invoke
///            ^^^^^^^
///            Response is here.
/// </summary>
public static class ChatClientResponses
{
  private static long _tokensCount = 0;
  private const long MaxTokens = 2000;  // this is a demo - real apps would have much higher limits

  /// <summary>
  /// Enforces a cumulative token budget across all LLM round-trips.
  ///
  /// Pre-flight guard: if the budget is already exhausted, returns a synthetic
  /// ChatResponse immediately — innerClient is never called, no tokens are consumed.
  /// Otherwise calls innerClient, silently accumulates the round-trip token cost,
  /// and returns the response unmodified.
  ///
  /// ChatClient-exclusive capability: response.Usage is present on ChatResponse
  /// (returned by the LLM provider) but is NOT available on AgentResponse (returned
  /// by the Agent layer). Token budget capping must therefore live at this layer.
  /// </summary>
  public static async Task<ChatResponse> EnforceTokenBudget(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerClient,
    CancellationToken cancellationToken)
  {
    var currentTokens = Interlocked.Read(ref _tokensCount);
    if (currentTokens > MaxTokens)
    {
      ColorHelper.PrintColoredLine($"[ChatClient] [Response] [Tokens] Budget exhausted ({_tokensCount} / {MaxTokens}) — LLM call skipped", ConsoleColor.Red);
      return new ChatResponse([new ChatMessage(ChatRole.Assistant, "Token budget exhausted.")]);
    }

    var response = await innerClient.GetResponseAsync(messages, options, cancellationToken);

    if (response.Usage is not null)
    {
      var totalTokens = response.Usage.TotalTokenCount
        ?? (response.Usage.InputTokenCount ?? 0) + (response.Usage.OutputTokenCount ?? 0);
      Interlocked.Add(ref _tokensCount, totalTokens);
    }

    return response;
  }

  /// <summary>
  /// Prefixes every assistant response with a UTC datetime stamp, like a board journal entry.
  ///
  /// Example: "[2025-01-15 14:30:00 UTC] Move forward, then turn left."
  ///
  /// Requires ChatResponse — only this middleware type can read and rewrite the
  /// response text. The stamp is applied after the LLM replies, so it never affects
  /// the prompt or token consumption.
  ///
  /// Story 3: The Audit Trail
  /// Operations teams need to know exactly when each LLM response was received.
  /// This middleware provides a reliable, tamper-evident datetime on every entry.
  /// </summary>
  public static async Task<ChatResponse> AddTimestamp(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerClient,
    CancellationToken cancellationToken)
  {
    ChatResponse response = await innerClient.GetResponseAsync(messages, options, cancellationToken);

    foreach (ChatMessage message in response.Messages)
    {
      if (!string.IsNullOrEmpty(message.Text))
      {
        string timestamp = DateTimeOffset.UtcNow.ToString("o");
        ColorHelper.PrintColoredLine($"[ChatClient] [Response] [Timestamp] Stamping response with [{timestamp}]", ConsoleColor.Cyan);
        message.Contents = [new TextContent($"[{timestamp}] {message.Text}")];
      }
    }

    return response;
  }
}
