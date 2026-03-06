using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Providers;

public class FileBasedContextProvider(string contextFilePath) : AIContextProvider
{
  protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
  {
    if (!File.Exists(contextFilePath))
    {
      return new AIContext
      {
        Instructions = "",
        Messages = [],
        Tools = []
      };
    }

    var contextContent = await File.ReadAllTextAsync(contextFilePath, cancellationToken);

    return new AIContext
    {
      Instructions = "Respond with JSON",
      Messages = [new ChatMessage(ChatRole.System, contextContent)],
      Tools = [],
    };
  }
}
