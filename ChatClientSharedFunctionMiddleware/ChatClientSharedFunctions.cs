using Helpers;
using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;

namespace Middleware;

/// <summary>
/// ChatClient SharedFunction Middleware — Step 1 of 3: Prepare
///
/// Pattern: Use(Func&lt;messages, options, next, ct, Task&gt;)
///
/// Executes BEFORE and AFTER every LLM call.
/// Sits OUTERMOST in the pipeline — first in, last out.
///
/// Key characteristics:
/// - Fires once per GetResponseAsync call (= once per LLM round-trip).
///   With tool-calling agents, a single user query triggers multiple round-trips,
///   so limit counters and sanitization re-apply on every round-trip automatically.
/// - Blind to ChatResponse: next returns Task, not Task&lt;ChatResponse&gt;.
///   This makes SharedFunction input-focused by design — it cannot inspect
///   or modify the response.
/// - No session context, no agent identity — universal across all agents.
///
/// Recommended position: OUTERMOST (.Use first, before Response and FunctionCalling).
/// Reason: fail fast before any LLM work; sanitize input before Response sees it.
///
/// prepare → handle → invoke
/// ^^^^^^^^
/// SharedFunction is here.
///
/// Perfect for:
/// - Story 1: Request limit enforcement (prevent runaway costs)
/// - Story 2: Universal email removal (GDPR compliance)
/// - Global rate limiting
/// </summary>
public static class ChatClientSharedFunctions
{
  private static int _requestCount = 0;
  private const int MaxRequests = 2; // very few for demo purposes — one round-trip per query with JSON instruction
  private const string EmailPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
  private const string EmailMask = "[REDACTED-EMAIL]";

  /// <summary>
  /// Limits the number of LLM requests for the app instance lifetime to prevent runaway costs.
  ///
  /// Fires once per LLM round-trip (per GetResponseAsync call).
  /// With tool-calling agents, a single user query triggers MULTIPLE round-trips:
  /// one for the initial LLM call, one per tool-result round-trip.
  /// The limit therefore depletes faster than the number of user queries suggests.
  ///
  /// Story 1: The $10,000 Weekend
  /// A startup deployed their agent on Friday. By Monday: $10,847 bill.
  /// This middleware prevents that by counting each LLM round-trip.
  /// </summary>
  public static async Task LimitRequests(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task> next,
    CancellationToken cancellationToken)
  {
    var currentCount = Interlocked.Increment(ref _requestCount);

    ColorHelper.PrintColoredLine($"[ChatClient] [SharedFunction] [Limit] PRE: Request {currentCount} of {MaxRequests} max", ConsoleColor.Yellow);

    // Check limit - block if exceeded
    if (currentCount > MaxRequests)
    {
      throw new LimitExceededException(currentCount, MaxRequests);
    }

    // Continue pipeline
    await next(messages, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[ChatClient] [SharedFunction] [Limit] POST: Request {currentCount} of {MaxRequests} max completed", ConsoleColor.Yellow);
  }

  /// <summary>
  /// Removes email addresses from all user messages before sending to LLM.
  ///
  /// Fires once per LLM round-trip. Sanitization is TRANSIENT: the LLM sees
  /// clean data on every call, but the agent's session chat history retains the
  /// original text. This is safe because the middleware re-applies on every round-trip.
  ///
  /// Contrast with Agent.RemoveEmail: that version is PERSISTENT — sanitized messages
  /// are stored in the session history, so all future round-trips already see clean data
  /// without re-sanitization. At the ChatClient layer, the session is not accessible,
  /// so transient re-application is the only option — and it is sufficient.
  ///
  /// Only user messages are sanitized. Tool calls and tool results pass through unchanged:
  /// modifying them would break the LLM's function-calling protocol.
  ///
  /// Story 2: The GDPR Nightmare
  /// A healthcare company's AI sent patient data to their LLM provider.
  /// This is the UNIVERSAL failsafe that prevents that.
  /// </summary>
  public static async Task RemoveEmail(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task> next,
    CancellationToken cancellationToken)
  {
    ColorHelper.PrintColoredLine($"[ChatClient] [SharedFunction] [Email] PRE: Scanning {messages.Count()} messages...", ConsoleColor.Yellow);

    bool emailFound = false;
    List<ChatMessage> sanitizedMessages = [];

    foreach (var message in messages)
    {
      if (message.Role == ChatRole.User && message.Text is not null)
      {
        var sanitized = Regex.Replace(message.Text, EmailPattern, EmailMask);
        if (sanitized != message.Text) emailFound = true;
        sanitizedMessages.Add(new ChatMessage(message.Role, sanitized));
      }
      else
      {
        sanitizedMessages.Add(message);
      }
    }

    ColorHelper.PrintColoredLine(
      emailFound
        ? "[ChatClient] [SharedFunction] [Email] Email detected and removed!"
        : "[ChatClient] [SharedFunction] [Email] No email found",
      emailFound
        ? ConsoleColor.DarkYellow
        : ConsoleColor.Yellow);

    await next(sanitizedMessages, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[ChatClient] [SharedFunction] [Email] POST: Completed", ConsoleColor.Yellow);
  }
}
