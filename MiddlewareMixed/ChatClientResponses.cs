using Helpers;
using Microsoft.Extensions.AI;

namespace Middleware;

public static class ChatClientResponses
{
  private static long _tokensCount = 0;
  private const long MaxTokens = 2000;  // this is a demo - real apps would have much higher limits

  public static async Task<ChatResponse> EnforceTokenBudget(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerClient,
    CancellationToken cancellationToken)
  {
    var currentTokens = Interlocked.Read(ref _tokensCount);
    if (currentTokens > MaxTokens)
    {
      ColorHelper.PrintColoredLine($"[ChatClient] [Response] [Tokens] " +
        $"Budget exhausted ({_tokensCount} / {MaxTokens}) — LLM call skipped", ConsoleColor.Red);
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
        ColorHelper.PrintColoredLine($"[ChatClient] [Response] [Timestamp] Stamping response with [{timestamp}]", ConsoleColor.Yellow);
        message.Contents = [new TextContent($"[{timestamp}] {message.Text}")];
      }
    }

    return response;
  }
}
