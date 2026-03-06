using Microsoft.Extensions.VectorData;

namespace Providers;

public class ChatHistoryItem
{
  [VectorStoreKey]
  public string? Key { get; set; }

  [VectorStoreData]
  public string? SessionId { get; set; }

  [VectorStoreData]
  public DateTimeOffset? Timestamp { get; set; }

  [VectorStoreData]
  public string? SerializedMessage { get; set; }

  [VectorStoreData]
  public string? MessageText { get; set; }
}