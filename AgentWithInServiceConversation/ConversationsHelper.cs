using OpenAI.Conversations;
using System.ClientModel;
using System.Text.Json.Nodes;

namespace Helpers;

#pragma warning disable OPENAI001

public static class ConversationsHelper
{
  public static async Task<string> CreateAndGetConversationIdAsync(ConversationClient conversationClient)
  {
    ClientResult result = await conversationClient.CreateConversationAsync(BinaryContent.Create(BinaryData.FromString("{}")));
    return JsonNode.Parse(result.GetRawResponse().Content)!["id"]!.GetValue<string>();
  }

  public static async Task PrintConversationAsync(ConversationClient conversationClient, string conversationId)
  {
    var pages = conversationClient.GetConversationItemsAsync(conversationId);

    await foreach (ClientResult result in pages.GetRawPagesAsync())
    {
      if (JsonNode.Parse(result.GetRawResponse().Content)?["data"] is not JsonArray data) 
        continue;

      foreach (var message in data)
      {
        ColorHelper.PrintColoredLine($"  {message!["role"]!.GetValue<string>()} [{message["id"]!.GetValue<string>()}]:", ConsoleColor.White);

        if (message["content"] is JsonArray content)
          foreach (var contentItem in content)
            if (contentItem?["text"] is JsonNode text)
              ColorHelper.PrintColoredLine($"{text.GetValue<string>()}", ConsoleColor.Yellow);

        Console.WriteLine();
      }
    }
  }

  public static async Task DeleteConversationAsync(ConversationClient conversationClient, string conversationId)
  {
    ClientResult result = await conversationClient.DeleteConversationAsync(conversationId);
    bool deleted = JsonNode.Parse(result.GetRawResponse().Content)?["deleted"] is JsonValue;
    ColorHelper.PrintColoredLine($"  Deleted: {deleted}", ConsoleColor.Yellow);
  }
}