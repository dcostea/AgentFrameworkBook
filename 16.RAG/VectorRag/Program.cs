using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using RagShared;

Console.WriteLine("Try: How long before Robby's wheel adjustment record needs another review?");
IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string apiKey = configuration["OpenAI:ApiKey"] ??
    throw new InvalidOperationException("Set the OpenAI:ApiKey user secret.");
string embeddingModel = Environment.GetEnvironmentVariable("RAG_EMBEDDING_MODEL") ??
    configuration["OpenAI:EmbeddingModelId"] ??
    throw new InvalidOperationException("Set the OpenAI:EmbeddingModelId user secret.");

using IEmbeddingGenerator<string, Embedding<float>> embeddings = new OpenAIClient(apiKey)
    .GetEmbeddingClient(embeddingModel)
    .AsIEmbeddingGenerator();
string connectionString = Environment.GetEnvironmentVariable("RAG_POSTGRES_CONNECTION_STRING") ??
    configuration["Postgres:ConnectionString"] ??
    throw new InvalidOperationException("Set the Postgres:ConnectionString user secret or scoped RAG_POSTGRES_CONNECTION_STRING.");
await using VectorSearch vectors = new(connectionString, embeddings, embeddingModel);
using var ingestionTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
int updated = await vectors.IndexAsync(SampleData.Documents, ingestionTimeout.Token);
Console.WriteLine($"Persistent pgvector corpus ready: {updated} new or changed documents embedded.");

Console.WriteLine("\n2. Vector RAG — meaning rather than exact words");
Console.WriteLine("Enter a question; an empty line exits.");

bool retrieveOnly = args.Contains("--retrieve-only", StringComparer.Ordinal);
using IChatClient? chatClient = retrieveOnly
    ? null
    : new OpenAIClient(apiKey)
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

        IReadOnlyList<SearchHit> hits = await vectors.SearchAsync(
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
