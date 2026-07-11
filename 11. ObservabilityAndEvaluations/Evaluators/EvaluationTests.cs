using AITools;
using Evaluators.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.Text.Json;
using Xunit;

#pragma warning disable AIEVAL001

namespace Evaluators;

public class EvaluationTests
{
  private readonly IChatClient _chatClient;

  public EvaluationTests()
  {
    IConfiguration configuration = new ConfigurationBuilder().AddUserSecrets<EvaluationTests>().Build();

    string? model = configuration["OpenAI:ModelId"];
    string? apiKey = configuration["OpenAI:ApiKey"];

    _chatClient = new OpenAIClient(apiKey)
      .GetChatClient(model)
      .AsIChatClient();
  }

  [Theory]
  [InlineData("robby-intent-001")]
  [InlineData("robby-intent-002")]
  [InlineData("robby-intent-003")]
  public async Task IntentResolutionEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("intent-resolution.jsonl")
      .Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, record.FinalResponse));

    IntentResolutionEvaluator intentResolutionEvaluator = new();
    EvaluationResult result = await intentResolutionEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient));
    NumericMetric intentResolution = result.Get<NumericMetric>(IntentResolutionEvaluator.IntentResolutionMetricName);

    Assert.NotNull(intentResolution.Interpretation);
    Assert.Equal(record.ShouldPass, intentResolution.Interpretation.Rating >= EvaluationRating.Good);
  }

  [Theory]
  [InlineData("robby-coherence-001")]
  [InlineData("robby-coherence-002")]
  public async Task CoherenceEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("coherence.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, record.FinalResponse));

    CoherenceEvaluator coherenceEvaluator = new();
    EvaluationResult result = await coherenceEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient));
    NumericMetric coherence = result.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);

    Assert.NotNull(coherence.Interpretation);
    Assert.Equal(record.ShouldPass, coherence.Interpretation.Rating >= EvaluationRating.Good);
  }

  [Theory]
  [InlineData("robby-relevance-001")]
  [InlineData("robby-relevance-002")]
  public async Task RelevanceEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("relevance.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, record.FinalResponse));

    RelevanceEvaluator relevanceEvaluator = new();
    EvaluationResult result = await relevanceEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient));
    NumericMetric relevance = result.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);

    Assert.NotNull(relevance.Interpretation);
    Assert.Equal(record.ShouldPass, relevance.Interpretation.Rating >= EvaluationRating.Good);
  }

  [Theory]
  [InlineData("robby-fluency-001")]
  [InlineData("robby-fluency-002")]
  public async Task FluencyEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("fluency.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, record.FinalResponse));

    FluencyEvaluator fluencyEvaluator = new();
    EvaluationResult result = await fluencyEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient));
    NumericMetric fluency = result.Get<NumericMetric>(FluencyEvaluator.FluencyMetricName);

    Assert.NotNull(fluency.Interpretation);
    Assert.Equal(record.ShouldPass, fluency.Interpretation.Rating >= EvaluationRating.Average);
  }

  // Evaluators with additional context

  [Theory]
  [InlineData("robby-groundedness-001")]
  [InlineData("robby-groundedness-002")]
  public async Task GroundednessEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("groundedness.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, record.FinalResponse));
    GroundednessEvaluatorContext context = new(record.GroundTruth!);

    GroundednessEvaluator groundednessEvaluator = new();
    EvaluationResult result = await groundednessEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient), [context]);
    NumericMetric groundedness = result.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);

    Assert.NotNull(groundedness.Interpretation);
    Assert.Equal(record.ShouldPass, groundedness.Interpretation.Rating >= EvaluationRating.Good);
  }

  [Theory]
  [InlineData("robby-equivalence-001")]
  [InlineData("robby-equivalence-002")]
  public async Task EquivalenceEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("equivalence.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, record.FinalResponse));
    EquivalenceEvaluatorContext context = new(record.GroundTruth!);

    EquivalenceEvaluator equivalenceEvaluator = new();
    EvaluationResult result = await equivalenceEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient), [context]);
    NumericMetric equivalence = result.Get<NumericMetric>(EquivalenceEvaluator.EquivalenceMetricName);

    Assert.NotNull(equivalence.Interpretation);
    Assert.Equal(record.ShouldPass, equivalence.Interpretation.Rating >= EvaluationRating.Good);
  }

  [Theory]
  [InlineData("robby-completeness-001")]
  [InlineData("robby-completeness-002")]
  public async Task CompletenessEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("completeness.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, record.FinalResponse));
    CompletenessEvaluatorContext context = new(record.GroundTruth!);

    CompletenessEvaluator completenessEvaluator = new();
    EvaluationResult result = await completenessEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient), [context]);
    NumericMetric completeness = result.Get<NumericMetric>(CompletenessEvaluator.CompletenessMetricName);

    Assert.NotNull(completeness.Interpretation);
    Assert.Equal(record.ShouldPass, completeness.Interpretation.Rating >= EvaluationRating.Good);
  }

  [Theory]
  [InlineData("robby-retrieval-001")]
  [InlineData("robby-retrieval-002")]
  public async Task RetrievalEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("retrieval.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, record.FinalResponse));
    RetrievalEvaluatorContext context = new(record.RetrievedContextChunks!);

    RetrievalEvaluator retrievalEvaluator = new();
    EvaluationResult result = await retrievalEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient), [context]);
    NumericMetric retrieval = result.Get<NumericMetric>(RetrievalEvaluator.RetrievalMetricName);

    Assert.NotNull(retrieval.Interpretation);
    Assert.Equal(record.ShouldPass, retrieval.Interpretation.Rating >= EvaluationRating.Good);
  }

  // Composite evaluators

  [Theory]
  [InlineData("robby-composite-001")]
  [InlineData("robby-composite-002")]
  public async Task CompositeEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("composite.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, record.FinalResponse));

    CoherenceEvaluator coherenceEvaluator = new();
    EquivalenceEvaluator equivalenceEvaluator = new();
    CompositeEvaluator compositeEvaluator = new(coherenceEvaluator, equivalenceEvaluator);

    EquivalenceEvaluatorContext baselineResponseForEquivalence = new(record.GroundTruth!);

    EvaluationResult result = await compositeEvaluator.EvaluateAsync(history, response, 
      new ChatConfiguration(_chatClient),
      [baselineResponseForEquivalence]);
    NumericMetric coherence = result.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);
    NumericMetric equivalence = result.Get<NumericMetric>(EquivalenceEvaluator.EquivalenceMetricName);

    Assert.NotNull(coherence.Interpretation);
    Assert.Equal(record.ShouldPass, coherence.Interpretation.Rating >= EvaluationRating.Good);

    Assert.NotNull(equivalence.Interpretation);
    Assert.Equal(record.ShouldPass, equivalence.Interpretation.Rating >= EvaluationRating.Good);
  }

  // Evaluators with tools

  [Theory]
  [InlineData("robby-task-001")]
  [InlineData("robby-task-002")]
  [InlineData("robby-task-003")]
  [InlineData("robby-task-004")]
  public async Task TaskAdherenceEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("task-adherence.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    List<AIContent> toolCallContents = [.. record.ToolCalls
      .Select((tc, index) =>
      {
        AIFunctionArguments arguments = new(tc.Arguments.ToDictionary(kv => kv.Key,
          kv => kv.Value is JsonElement element ? element.Deserialize<object>() : kv.Value));

        return new FunctionCallContent($"call-{index + 1}", tc.Name, arguments);
      })];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, toolCallContents));

    TaskAdherenceEvaluatorContext context = new([.. MotorTools.AsAITools()]);

    TaskAdherenceEvaluator taskAdherenceEvaluator = new();
    EvaluationResult result = await taskAdherenceEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient), additionalContext: [context]);
    NumericMetric taskAdherence = result.Get<NumericMetric>(TaskAdherenceEvaluator.TaskAdherenceMetricName);

    Assert.NotNull(taskAdherence.Interpretation);
    Assert.Equal(record.ShouldPass, taskAdherence.Interpretation.Rating >= EvaluationRating.Good);
  }

  [Theory]
  [InlineData("robby-tool-001")]
  [InlineData("robby-tool-002")]
  [InlineData("robby-tool-003")]
  [InlineData("robby-tool-004")]
  public async Task ToolCallAccuracyEvaluator_MatchesExpectedBehavior(string id)
  {
    EvaluationRecord record = LoadEvaluationRecords("tool-call-accuracy.jsonl").Single(r => r.Id == id);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    List<AIContent> toolCallContents = [.. record.ToolCalls
      .Select((tc, index) =>
      {
        AIFunctionArguments arguments = new(tc.Arguments.ToDictionary(kv => kv.Key,
          kv => kv.Value is JsonElement element ? element.Deserialize<object>() : kv.Value));

        return new FunctionCallContent($"call-{index + 1}", tc.Name, arguments);
      })];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, toolCallContents));

    ToolCallAccuracyEvaluatorContext context = new([.. MotorTools.AsAITools()]);
    ToolCallAccuracyEvaluator toolCallAccuracyEvaluator = new();
    EvaluationResult result = await toolCallAccuracyEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient), additionalContext: [context]);
    BooleanMetric toolAccuracy = result.Get<BooleanMetric>(ToolCallAccuracyEvaluator.ToolCallAccuracyMetricName);

    Assert.NotNull(toolAccuracy.Interpretation);
    Assert.Equal(record.ShouldPass, toolAccuracy.Interpretation.Rating >= EvaluationRating.Good);
    Assert.Equal(record.ShouldPass, toolAccuracy.Value);
  }

  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

  private static IEnumerable<EvaluationRecord> LoadEvaluationRecords(string fileName) =>
    File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Data", fileName))
      .Select((line, index) => JsonSerializer.Deserialize<EvaluationRecord>(line, JsonOptions)
        ?? throw new InvalidDataException($"Could not deserialize {fileName} line {index + 1}."));
}
