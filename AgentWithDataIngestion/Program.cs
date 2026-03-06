using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI;

// https://github.com/microsoft/markitdown
// https://devblogs.microsoft.com/dotnet/introducing-data-ingestion-building-blocks-preview/
// https://github.com/dotnet/extensions

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"]!;
var apiKey = configuration["OpenAI:ApiKey"]!;
IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient()
.AsBuilder()
.Build();

var embeddingModel = configuration["OpenAI:EmbeddingModelId"]!;
var embeddingGenerator = new OpenAIClient(apiKey)
  .GetEmbeddingClient(embeddingModel)
  .AsIEmbeddingGenerator();

// Configure document processor
EnricherOptions enricherOptions = new(chatClient)
{
  // Enricher failures should not fail the whole ingestion pipeline, as they are best-effort enhancements.
  // This logger factory can be used to create loggers to log such failures.
  LoggerFactory = loggerFactory
};

IngestionDocumentProcessor imageAlternativeTextEnricher = new ImageAlternativeTextEnricher(enricherOptions);

// Configure chunker to split text into semantic chunks
IngestionChunkerOptions chunkerOptions = new(TiktokenTokenizer.CreateForModel(model))
{
  MaxTokensPerChunk = 150,
  OverlapTokens = 20,
};

IngestionChunker<string> chunker = new SemanticSimilarityChunker(embeddingGenerator, chunkerOptions);

// Configure chunk processor to generate summaries for each chunk
IngestionChunkProcessor<string> summaryEnricher = new SummaryEnricher(enricherOptions);

// Configure SQLite Vector Store
////using SqliteVectorStore vectorStore = new(
////  "Data Source=vectors.db;Pooling=false",
////  new()
////  {
////    EmbeddingGenerator = embeddingGenerator
////  });

// Configure In-Memory Vector Store
using InMemoryVectorStore vectorStore = new (new() { EmbeddingGenerator = embeddingGenerator });

// Configure document reader Markdown (md) or MarkItDown (docx, pdf, etc.)
IngestionDocumentReader reader = new MarkdownReader();
////IngestionDocumentReader reader = new MarkItDownReader();

// The writer requires the embedding dimension count to be specified.
// For OpenAI's `text-embedding-3-small`, the dimension count is 1536.
using VectorStoreWriter<string> writer = new(vectorStore, dimensionCount: 1536, new VectorStoreWriterOptions { CollectionName = "data" });

// Compose data ingestion pipeline
using IngestionPipeline<string> pipeline = new(reader, chunker, writer, loggerFactory: loggerFactory)
{
  DocumentProcessors = { imageAlternativeTextEnricher },
  ChunkProcessors = { summaryEnricher },
};

int processedCount = 0;
int successCount = 0;

await foreach (var result in pipeline.ProcessAsync(new DirectoryInfo("./data"), searchPattern: "*.md"))
{
  processedCount++;
  if (result.Succeeded)
  {
    successCount++;
    Console.WriteLine($"Completed processing '{result.DocumentId}'. Succeeded: '{result.Succeeded}'.");
  }
  else
  {
    Console.WriteLine($"Failed processing '{result.DocumentId}'. Exception: {result.Exception?.Message}");
  }
}

Console.WriteLine($"\nProcessed {processedCount} documents, {successCount} succeeded.\n");

if (processedCount == 0)
{
  Console.WriteLine("No documents were processed. Please check if the './data' directory contains valid files.");
  return;
}

if (successCount == 0)
{
  Console.WriteLine("No documents were successfully processed. Check the error messages above for details.");
  return;
}

// Search the vector store collection and display results
var collection = writer.VectorStoreCollection;

while (true)
{
  Console.Write("Enter your question (or 'exit' to quit): ");
  string? searchValue = Console.ReadLine();
  if (string.IsNullOrEmpty(searchValue) || searchValue == "exit")
  {
    break;
  }

  Console.WriteLine("Searching...\n");
  await foreach (var result in collection.SearchAsync(searchValue, top: 3))
  {
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine($"Score: {result.Score}");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"Content: {result.Record["content"]}\n");
    Console.ResetColor();
  }
}
