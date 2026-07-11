using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using System.Collections;
using System.Text.Json;

namespace Providers;

public class VectorStoreChatHistoryProvider : ChatHistoryProvider, IReadOnlyList<ChatMessage>
{
  private readonly VectorStore _vectorStore;
  private readonly int _topResults;

  public List<ChatMessage> ChatMessages { get; private set; } = [];

  public int Count => ChatMessages.Count;

  public ChatMessage this[int index] => ChatMessages[index];

  public string? SessionId { get; private set; }

  public VectorStoreChatHistoryProvider(VectorStore vectorStore, JsonElement serializedStoreState, int topResults = 10)
  {
    _vectorStore = vectorStore;
    _topResults = topResults;

    if (serializedStoreState.ValueKind is JsonValueKind.String)
    {
      // Here we can deserialize the session id so that we can access the same messages as before the suspension.
      SessionId = serializedStoreState.Deserialize<string>();
    }
  }
  
  protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrEmpty(SessionId))
    {
      return [];
    }

    var collection = _vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory");
    await collection.EnsureCollectionExistsAsync(cancellationToken);

    var messages = await collection
      .GetAsync(item => item.SessionId == SessionId, _topResults, new() { OrderBy = order => order.Ascending(item => item.Timestamp) }, cancellationToken)
      .Select(item => JsonSerializer.Deserialize<ChatMessage>(item.SerializedMessage!)!)
      .ToListAsync(cancellationToken);

    return messages;
  }

  protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
  {
    SessionId ??= Guid.NewGuid().ToString();

    var collection = _vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory");
    await collection.EnsureCollectionExistsAsync(cancellationToken);

    // Add both request and response messages to the store
    var newMessages = context.RequestMessages.Concat(context.ResponseMessages ?? []);

    await collection.UpsertAsync(newMessages.Select(item => new ChatHistoryItem()
    {
      Key = SessionId + (item.MessageId ?? Guid.NewGuid().ToString()),
      Timestamp = DateTimeOffset.UtcNow,
      SessionId = SessionId,
      SerializedMessage = JsonSerializer.Serialize(item),
      MessageText = item.Text
    }), cancellationToken);

    // Update ChatMessages with the newly persisted messages
    ChatMessages.AddRange(newMessages);
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
