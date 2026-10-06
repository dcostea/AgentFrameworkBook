using Microsoft.Extensions.AI;

namespace RagSamples.Tests;

// These test-only vectors exercise retrieval mechanics, not real semantic relevance.
internal sealed class TestEmbeddingGenerator(Func<string, float[]> vectorFor)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    public List<string[]> Calls { get; } = [];
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] inputs = values.ToArray();
        Calls.Add(inputs);
        LastCancellationToken = cancellationToken;
        GeneratedEmbeddings<Embedding<float>> embeddings =
            new(inputs.Select(input => new Embedding<float>(vectorFor(input))));
        return Task.FromResult(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}

internal sealed class RecordingChatClient(string response) : IChatClient
{
    public Exception? Failure { get; init; }
    public int CallCount { get; private set; }
    public ChatMessage[] Messages { get; private set; } = [];
    public ChatOptions? Options { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        Messages = messages.ToArray();
        Options = options;
        LastCancellationToken = cancellationToken;
        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("RagAnswer is expected to use non-streaming responses.");

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
