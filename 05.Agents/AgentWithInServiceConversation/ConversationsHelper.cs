using OpenAI.Chat;
using OpenAI.Conversations;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Helpers;

#pragma warning disable OPENAI001

public static class ConversationsHelper
{
  public static async Task<string> CreateAndGetIdAsync(ConversationClient conversationClient)
  {
    ConversationResource conversation = await conversationClient
      .CreateConversationAsync(new ConversationCreationOptions());
    return conversation.Id;
  }

  // This method prints the conversation items for a given conversation ID using the provided ConversationClient.
  public static async Task PrintAsync(ConversationClient conversationClient, string conversationId)
  {
    var pages = conversationClient.GetConversationItemsAsync(conversationId);

    await foreach (ClientResult result in pages.GetRawPagesAsync())
    {
      var page = result.GetRawResponse().Content.ToObjectFromJson<ConversationItemsPage>(SerializerOptions)!;
      
      foreach (MessageResponseItem message in page.Data.OfType<MessageResponseItem>())
      {
        ColorHelper.PrintColoredLine($"  {message.Role} [{message.Id}]:", ConsoleColor.White);
        foreach (ResponseContentPart content in message.Content)
          ColorHelper.PrintColoredLine(content.Text, ConsoleColor.Yellow);
        Console.WriteLine();
      }
    }
  }

  public static async Task DeleteAsync(ConversationClient conversationClient, string conversationId)
  {
    ClientResult<ConversationDeletionResult> result = await conversationClient.DeleteConversationAsync(conversationId);
    ColorHelper.PrintColoredLine($"  Deleted: {result.Value?.Deleted}", ConsoleColor.Yellow);
  }

  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
  {
    Converters = { new JsonModelConverter() }
  };

  private sealed class ConversationItemsPage
  {
    public required List<ResponseItem> Data { get; init; }
  }
}