using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OpenAI;
using RagShared;

const int candidatesPerRetriever = 5;
const int fusedCandidateCount = 6;
const int contextCount = 3;

Console.WriteLine("Try: When does Robby's wheel calibration record expire?");
Console.WriteLine("Add --rerank to compare RRF with optional LLM reranking.");
await using McpClient toolbox = await McpSearch.ConnectAsync();

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string apiKey = configuration["OpenAI:ApiKey"] ??
    throw new InvalidOperationException("Set the OpenAI:ApiKey user secret.");
string model = configuration["OpenAI:ModelId"] ??
    throw new InvalidOperationException("Set the OpenAI:ModelId user secret.");
string embeddingModel = Environment.GetEnvironmentVariable("RAG_EMBEDDING_MODEL") ??
    configuration["OpenAI:EmbeddingModelId"] ??
    throw new InvalidOperationException("Set the OpenAI:EmbeddingModelId user secret.");
OpenAIClient openAIClient = new(apiKey);

using IEmbeddingGenerator<string, Embedding<float>> embeddings = openAIClient
    .GetEmbeddingClient(embeddingModel)
    .AsIEmbeddingGenerator();
using IChatClient? reranker = args.Contains("--rerank", StringComparer.Ordinal)
    ? openAIClient.GetChatClient(model).AsIChatClient()
    : null;
string connectionString = Environment.GetEnvironmentVariable("RAG_POSTGRES_CONNECTION_STRING") ??
    configuration["Postgres:ConnectionString"] ??
    throw new InvalidOperationException("Set the Postgres:ConnectionString user secret or scoped RAG_POSTGRES_CONNECTION_STRING.");
await using VectorSearch vectors = new(connectionString, embeddings, embeddingModel);
using var ingestionTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
int updated = await vectors.IndexAsync(SampleData.Documents, ingestionTimeout.Token);
Console.WriteLine($"Persistent pgvector corpus ready: {updated} new or changed documents embedded.");

Console.WriteLine("\n4. Hybrid RAG — BM25 + vector → RRF → optional LLM reranking");
Console.WriteLine("Enter a question; an empty line exits.");

bool retrieveOnly = args.Contains("--retrieve-only", StringComparer.Ordinal);
using IChatClient? chatClient = retrieveOnly
    ? null
    : openAIClient.GetChatClient(model).AsIChatClient();
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

        IReadOnlyList<Document> documents = await McpSearch.GetDocumentsAsync(toolbox, cancellation.Token);
        IReadOnlyList<SearchHit> bm25Hits = Bm25Search.Search(documents, question, candidatesPerRetriever);

        IReadOnlyList<SearchHit> vectorHits = await vectors.SearchAsync(
            question,
            candidatesPerRetriever,
            cancellation.Token);

        ShowHits("BM25 candidates", bm25Hits);
        ShowHits("Vector candidates", vectorHits);

        IReadOnlyList<SearchHit> fused = RankFusion.Fuse(fusedCandidateCount, bm25Hits, vectorHits);
        ShowHits("RRF candidates", fused);

        IReadOnlyList<SearchHit> hits;
        if (reranker is null)
        {
            hits = fused.Take(contextCount).ToArray();
        }
        else
        {
            RerankResult reranked = await LlmReranker.RerankAsync(
                reranker,
                question,
                fused,
                contextCount,
                cancellation.Token);

            Console.WriteLine(reranked.Warning ??
                "LLM reranking complete: order may change; displayed scores remain RRF scores.");
            hits = reranked.Hits;
        }

        ShowHits("Selected context", hits);

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

static void ShowHits(string label, IReadOnlyList<SearchHit> hits)
{
    Console.WriteLine($"\n{label} ({hits.Count} hits)");

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
