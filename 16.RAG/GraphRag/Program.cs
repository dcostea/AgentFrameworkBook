using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OpenAI;
using RagShared;

Console.WriteLine("Try: What could be affected if SafetyService goes down?");
await using McpClient toolbox = await McpSearch.ConnectAsync();

Console.WriteLine("\n3. Graph RAG — following dependencies over MCP");
Console.WriteLine("Enter a question; an empty line exits.");

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

        string? entity = SelectGraphEntity(question);
        if (entity is null)
        {
            continue;
        }

        IReadOnlyList<SearchHit> hits = await McpSearch.GraphAsync(
            toolbox,
            entity,
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

static string? SelectGraphEntity(string question)
{
    string[] services = SampleData.Documents.Select(document => document.Service).ToArray();
    string[] mentioned = services
        .Where(service => question.Contains(service, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    if (mentioned.Length == 1)
    {
        Console.WriteLine($"Graph starting service: {mentioned[0]} (incoming dependencies, at most two hops)");
        return mentioned[0];
    }

    Console.WriteLine($"Choose a graph starting service: {string.Join(", ", services)}.");
    while (true)
    {
        Console.Write("Service (empty cancels this question): ");
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        string? match = services.FirstOrDefault(
            service => service.Equals(input.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            return match;
        }

        Console.WriteLine("Enter one of the full service names above.");
    }
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
