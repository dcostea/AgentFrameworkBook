using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OpenAI;
using RagShared;

bool retrieveOnly = args.Contains("--retrieve-only", StringComparer.Ordinal);
IConfigurationRoot configuration = new ConfigurationBuilder()
  .AddUserSecrets<Program>()
  .Build();

using IChatClient? chatClient = retrieveOnly
    ? null
    : new OpenAIClient(configuration["OpenAI:ApiKey"] ??
        throw new InvalidOperationException("Set the OpenAI:ApiKey user secret."))
        .GetChatClient(configuration["OpenAI:ModelId"] ??
            throw new InvalidOperationException("Set the OpenAI:ModelId user secret."))
        .AsIChatClient();
using CancellationTokenSource cancellation = new();

Console.WriteLine("Try: calibration");
await using McpClient toolbox = await McpSearch.ConnectAsync();

Console.WriteLine("\n1. Keyword RAG — PostgreSQL full-text search over MCP");
Console.WriteLine("Enter a question; an empty line exits.");


ConsoleCancelEventHandler cancel = (_, eventArgs) =>
{
  eventArgs.Cancel = true;
  cancellation.Cancel();
};

Console.CancelKeyPress += cancel;

try
{
  while (!cancellation.IsCancellationRequested)
  {
    Console.Write("\nQuestion: ");
    string? question = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(question))
    {
      break;
    }

    IReadOnlyList<SearchHit> hits = await McpSearch.KeywordsAsync(
        toolbox,
        question,
        top: 3,
        cancellation.Token);

    ShowHits(hits);

    if (hits.Count == 0)
    {
      Console.WriteLine("No supporting documents were retrieved; no model call was made.");
    }
    else if (chatClient is not null)
    {
      string answer = await RagAnswer.GenerateAsync(
          chatClient,
          question,
          hits,
          cancellation.Token);

      Console.WriteLine($"\nASSISTANT: {answer}");
    }
  }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
  Console.WriteLine("\nCancelled.");
}
finally
{
  Console.CancelKeyPress -= cancel;
}

static void ShowHits(IReadOnlyList<SearchHit> hits)
{
  Console.WriteLine($"\nSelected context ({hits.Count} hits)");

  int rank = 0;
  foreach (SearchHit hit in hits)
  {
    Console.WriteLine($"  {++rank}. [{hit.Id}] {hit.Title} | {hit.Source} | score={hit.Score:F4}");
    Console.WriteLine($"    {hit.Text}");

    if (!string.IsNullOrWhiteSpace(hit.Evidence))
    {
      Console.WriteLine($"    {hit.Evidence}");
    }
  }
}
