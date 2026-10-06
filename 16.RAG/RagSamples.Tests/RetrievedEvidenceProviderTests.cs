using Microsoft.Agents.AI;
using RagShared;
using Xunit;

namespace RagSamples.Tests;

public sealed class RetrievedEvidenceProviderTests
{
    [Fact]
    public async Task OneProvider_IsolatesSessionsAndReplacesEvidenceForEachRun()
    {
        using var client = new RecordingChatClient("Recorded answer");
        var provider = new RetrievedEvidenceProvider();
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions { AIContextProviders = [provider] });
        var first = await agent.CreateSessionAsync();
        var second = await agent.CreateSessionAsync();
        provider.SetEvidence(first, [new SearchHit { Id = "doc-2", Title = "Calibration", Text = "first-only", Score = 1, Source = "vector" }]);
        provider.SetEvidence(second, [new SearchHit { Id = "doc-3", Title = "Hazards", Text = "second-only", Score = 1, Source = "graph" }]);

        await agent.RunAsync("First question", first);
        Assert.Contains("first-only", client.Messages.Last().Text);
        Assert.DoesNotContain("second-only", string.Join("\n", client.Messages.Select(message => message.Text)));
        await agent.RunAsync("Second question", second);
        Assert.Contains("second-only", client.Messages.Last().Text);
        Assert.DoesNotContain("first-only", string.Join("\n", client.Messages.Select(message => message.Text)));

        provider.SetEvidence(first, [new SearchHit { Id = "doc-8", Title = "Maintenance", Text = "replacement-only", Score = 1, Source = "keyword" }]);
        await agent.RunAsync("New first question", first);
        Assert.Contains("replacement-only", client.Messages.Last().Text);
        Assert.DoesNotContain("first-only", client.Messages.Last().Text);
        // A reused session retains chat history; RagAnswer intentionally creates a fresh session per question.
        Assert.Contains("first-only", string.Join("\n", client.Messages.Select(message => message.Text)));
        Assert.DoesNotContain("second-only", string.Join("\n", client.Messages.Select(message => message.Text)));
        Assert.Equal(3, client.CallCount);
    }

    [Fact]
    public async Task Cancellation_IsPropagatedWithoutCallingTheModel()
    {
        using var client = new RecordingChatClient("Must not be used");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RagAnswer.GenerateAsync(client, "Question",
            [new SearchHit { Id = "doc-2", Title = "Calibration", Text = "Evidence", Score = 1, Source = "vector" }], cancellation.Token));
        Assert.Equal(0, client.CallCount);
    }
}
