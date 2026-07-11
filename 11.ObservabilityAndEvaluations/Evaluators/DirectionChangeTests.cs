using System.Text.Json;
using Evaluators.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Configuration;
using OpenAI;
using Xunit;

namespace Evaluators;

public class DirectionChangeTests
{
  private readonly IChatClient _chatClient;

  public DirectionChangeTests()
  {
    IConfiguration configuration = new ConfigurationBuilder().AddUserSecrets<DirectionChangeTests>().Build();

    string? model = configuration["OpenAI:ModelId"];
    string? apiKey = configuration["OpenAI:ApiKey"];

    _chatClient = new OpenAIClient(apiKey)
      .GetChatClient(model)
      .AsIChatClient();
  }

  [Theory]
  [InlineData("robby-direction-change-001")]
  [InlineData("robby-direction-change-002")]
  [InlineData("robby-direction-change-003")]
  public async Task DirectionChangeSafetyEvaluator_MatchesExpectedBehavior(string id)
  {
    DirectionChangeRecord record = LoadDirectionChangeRecords("direction-change.jsonl").Single(r => r.Id == id);
    List<string> toolNames = [.. record.ToolCalls.Select(toolCall => toolCall.Name)];
    (_, _, bool changed, string? from, string? to) = DirectionChangeEvaluator.AnalyzeDirectionChangeSafety(toolNames);

    Assert.Equal(record.ExpectedChanged, changed);
    Assert.Equal(record.ExpectedFrom, from);
    Assert.Equal(record.ExpectedTo, to);

    List<ChatMessage> history =
    [
      new(ChatRole.System, record.Agent.Instructions),
      new(ChatRole.User, record.UserInput)
    ];

    List<AIContent> toolCallContents = [.. record.ToolCalls
      .Select((tc, index) =>
      {
        AIFunctionArguments arguments = new(
          tc.Arguments.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is JsonElement element ? element.Deserialize<object>() : kv.Value));

        return new FunctionCallContent($"call-{index + 1}", tc.Name, arguments);
      })];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, toolCallContents));

    DirectionChangeEvaluator evaluator = new();
    EvaluationResult result = await evaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient));
    BooleanMetric directionChangeSafety = result.Get<BooleanMetric>(DirectionChangeEvaluator.DirectionChangeMetricName);

    Assert.NotNull(directionChangeSafety.Interpretation);
    Assert.Equal(!record.ExpectedChanged, directionChangeSafety.Value);
    Assert.Equal(record.ExpectedChanged, directionChangeSafety.Interpretation.Failed);
  }

  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

  private static IEnumerable<DirectionChangeRecord> LoadDirectionChangeRecords(string fileName) =>
    File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Data", fileName))
      .Where(line => !string.IsNullOrWhiteSpace(line))
      .Select((line, index) => JsonSerializer.Deserialize<DirectionChangeRecord>(line, JsonOptions)
        ?? throw new InvalidDataException($"Could not deserialize {fileName} line {index + 1}."));
}
