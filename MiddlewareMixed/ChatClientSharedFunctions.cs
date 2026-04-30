using Helpers;
using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;

namespace Middleware;

public static class ChatClientSharedFunctions
{
  private static int _requestCount = 0;
  private const int MaxRequests = 4; // very few for demo purposes — one round-trip per query with JSON instruction
  private const string EmailPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
  private const string EmailMask = "[REDACTED-EMAIL]";

  public static async Task LimitRequests(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task> next,
    CancellationToken cancellationToken)
  {
    var currentCount = Interlocked.Increment(ref _requestCount);

    ColorHelper.PrintColoredLine($"[ChatClient] [SharedFunction] [Limit] PRE: " +
      $"Request {currentCount} of {MaxRequests} max", ConsoleColor.Yellow);

    // Check limit - block if exceeded
    if (currentCount > MaxRequests)
    {
      throw new LimitExceededException(currentCount, MaxRequests);
    }

    // Continue pipeline
    await next(messages, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[ChatClient] [SharedFunction] [Limit] POST: " +
      $"Request {currentCount} of {MaxRequests} max completed", ConsoleColor.Yellow);
  }

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
        ? ConsoleColor.Red
        : ConsoleColor.Yellow);

    await next(sanitizedMessages, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[ChatClient] [SharedFunction] [Email] POST: Completed", ConsoleColor.Yellow);
  }
}
