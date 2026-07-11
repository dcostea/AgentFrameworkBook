using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Providers;

public class CustomFileBasedContextProvider(string instructionsFilePath, string chatHistory) : AIContextProvider
{
  protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
  {
    var instructions = await File.ReadAllTextAsync(instructionsFilePath, cancellationToken);

    var chatHistoryJson = await File.ReadAllTextAsync(chatHistory, cancellationToken);
    var messages = JsonSerializer.Deserialize<ChatMessage[]>(chatHistoryJson) ?? [];

    return new AIContext
    {
      Instructions = instructions,
      Messages = messages,
      Tools = [],
    };
  }

  protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
  {
    var instructions = await File.ReadAllTextAsync(instructionsFilePath, cancellationToken);

    string moreInstructions = """
      June 6, 2025 - Morning: 13°C, partly cloudy, wind 9 km/h, dry. Afternoon: 21°C, sunny, wind 11 km/h, dry roads. Night: 12°C, clear, wind 6 km/h, calm.
      June 7, 2025 - Morning: 14°C, sunny, wind 7 km/h, dry. Afternoon: 23°C, mostly sunny, wind 10 km/h, no rain. Night: 13°C, few clouds, wind 5 km/h, calm.
      """;

    var updatedInstructions = instructions + "\n" + moreInstructions;

    Console.WriteLine("[StoreAIContextAsync] Simulated file update (not written to disk yet, because we don't want to alter the original context file):");
    Console.WriteLine(updatedInstructions);
  }
}
