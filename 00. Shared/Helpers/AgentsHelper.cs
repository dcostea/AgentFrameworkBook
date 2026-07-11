using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Helpers;

public static class AgentsHelper
{
  public static void PrintTools(IList<ChatMessage> messages)
  {
    foreach (var message in messages)
    {
      foreach (var content in message.Contents)
      {
        switch (content)
        {
          case TextContent textContent:
            ColorHelper.PrintColoredLine($"ASST RESP: {textContent.Text}", ConsoleColor.Yellow);
            break;
          case FunctionCallContent toolCall:
            ColorHelper.PrintColoredLine($"TOOL CALL {toolCall.CallId}: {toolCall.Name} {JsonSerializer.Serialize(toolCall.Arguments)}", ConsoleColor.Cyan);
            break;
          case FunctionResultContent toolResponse:
            ColorHelper.PrintColoredLine($"TOOL RESP {toolResponse.CallId}: {toolResponse.Result}", ConsoleColor.Blue);
            break;
        }
      }
    }
  }

  public static async Task PrintChatMessagesAsync(AgentSession session)
  {
    if (session.StateBag.TryGetValue<InMemoryChatHistoryProvider.State>(nameof(InMemoryChatHistoryProvider), out var state))
    {
      foreach (ChatMessage message in state!.Messages)
      {
        var source = message.GetAgentRequestMessageSourceType();
        ColorHelper.PrintColoredLine($"[{source.Value}] {message.Role}: ", ConsoleColor.Yellow);
        ColorHelper.PrintColoredLine($"{message.Text}", ConsoleColor.White);
      }
    }
  }
}