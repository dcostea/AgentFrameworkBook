using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Collections;
using System.Text.Json;

namespace Providers;

public class CustomFileBasedChatHistoryProvider(string filePath) : ChatHistoryProvider, IReadOnlyList<ChatMessage>
{
  public List<ChatMessage> ChatMessages { get; private set; } = [];

  public int Count => ChatMessages.Count;

  public ChatMessage this[int index] => ChatMessages[index];

  protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
  {
    if (!File.Exists(filePath))
    {
      return [];
    }

    var json = await File.ReadAllTextAsync(filePath, cancellationToken);
    ChatMessages = JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? [];
    return ChatMessages;
  }

  protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
  {
    if (File.Exists(filePath))
    {
      var json = await File.ReadAllTextAsync(filePath, cancellationToken);
      ChatMessages = JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? [];
    }

    // Append new messages to the existing ones
    ChatMessages.AddRange(context.RequestMessages.Concat(context.ResponseMessages ?? []));

    // Save back to file
    var serialized = JsonSerializer.Serialize(ChatMessages, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(filePath, serialized, cancellationToken);
  }

  public IEnumerator<ChatMessage> GetEnumerator()
  {
    return ChatMessages.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }
}