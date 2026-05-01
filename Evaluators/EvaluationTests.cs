using AITools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.Configuration;
using OpenAI;
using Xunit;

namespace Evaluators;

public class EvaluationTests
{
  private readonly IChatClient _chatClient;

  public EvaluationTests()
  {
    var configuration = new ConfigurationBuilder().AddUserSecrets<EvaluationTests>().Build();

    var model = configuration["OpenAI:ModelId"];
    var apiKey = configuration["OpenAI:ApiKey"];

    _chatClient = new OpenAIClient(apiKey)
      .GetChatClient(model)
      .AsIChatClient();
  }

  [Fact]
  public async Task IntentResolution_IsHighForClearAnswer()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are: forward, backward, turn left, turn right, stop.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.

        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    ChatResponse response = new(
      new ChatMessage(ChatRole.Assistant, """
      1. turn right
      2. forward
      3. turn left
      4. forward
      5. turn left
      6. forward
      7. turn right
      """));

    ChatConfiguration chatConfiguration = new(_chatClient);

#pragma warning disable AIEVAL001
    IntentResolutionEvaluator evaluator = new();
    EvaluationResult result = await evaluator.EvaluateAsync(history, response, chatConfiguration);
    NumericMetric intentResolution = result.Get<NumericMetric>(IntentResolutionEvaluator.IntentResolutionMetricName);

    Assert.NotNull(intentResolution.Interpretation);
    Assert.Null(intentResolution.Interpretation!.Reason);
    Assert.True(intentResolution.Interpretation!.Rating >= EvaluationRating.Good);
    Assert.False(intentResolution.Interpretation!.Failed);
  }

  ////  [Fact]
  ////  public async Task RetrievalEvaluator_IsHighForRelevantChunks()
  ////  {
  ////    List<Microsoft.Extensions.AI.ChatMessage> history =
  ////    [
  ////      new(ChatRole.User, """
  ////      What are the main attractions in The Hague?
  ////      """)
  ////    ];

  ////    ChatResponse response = new(
  ////      new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, """
  ////      Some key attractions in The Hague include the Binnenhof, the Peace Palace, and Scheveningen beach.
  ////      """));

  ////    ChatConfiguration chatConfiguration = new(_chatClient);

  ////    RetrievalEvaluatorContext retrievalContext = new()
  ////    {
  ////      Name = "RagRun-Attractions",
  ////      RetrievedContextChunks =
  ////      [
  ////        new AIContent("The Binnenhof is the historic parliament complex located in The Hague."),
  ////      new AIContent("Scheveningen is a popular beach district in The Hague."),
  ////      new AIContent("The Peace Palace hosts the International Court of Justice in The Hague.")
  ////      ]
  ////    };

  ////#pragma warning disable AIEVAL001
  ////    var evaluator = new RetrievalEvaluator();
  ////    EvaluationResult result = await evaluator.EvaluateAsync(
  ////      history,
  ////      response,
  ////      chatConfiguration,
  ////      additionalContext: [retrievalContext]);

  ////    NumericMetric retrieval =
  ////      result.Get<NumericMetric>(RetrievalEvaluator.RetrievalMetricName);

  ////    Assert.NotNull(retrieval.Interpretation);
  ////    Assert.NotNull(retrieval.Interpretation!.Reason);
  ////    Assert.True(retrieval.Interpretation!.Rating >= EvaluationRating.Good);
  ////    Assert.False(retrieval.Interpretation!.Failed);
  ////  }

  [Fact]
  public async Task TaskAdherence_IsGood_WhenAgentFollowsTask()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are a financial assistant.
        Answer only questions about stock prices and simple summaries.
        Do not give medical or legal advice.
        """),
      new(ChatRole.User, """
        What is the current price of MSFT?
        """)
    ];

    ChatResponse response = new(
      new ChatMessage(ChatRole.Assistant, """
      The current price of MSFT is approximately 410 USD.
      """));

    ChatConfiguration chatConfiguration = new(_chatClient);
    TaskAdherenceEvaluatorContext context = new([.. MotorTools.AsAITools()]);

    #pragma warning disable AIEVAL001
    TaskAdherenceEvaluator evaluator = new();
    EvaluationResult result = await evaluator.EvaluateAsync(history, response, chatConfiguration, additionalContext: [context]);
    NumericMetric taskAdherence = result.Get<NumericMetric>(TaskAdherenceEvaluator.TaskAdherenceMetricName);

    Assert.NotNull(taskAdherence.Interpretation);
    Assert.Null(taskAdherence.Interpretation!.Reason);
    Assert.True(taskAdherence.Interpretation!.Rating >= EvaluationRating.Good);
    Assert.False(taskAdherence.Interpretation!.Failed);
  }

  [Fact]
  public async Task ToolCallAccuracy_IsTrue_ForCorrectToolUsage()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are forward, backward, turn left, turn right, and stop.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Stop immediatelly!"
        """)
    ];

    // In real code, this ChatResponse would include structured tool call(s),
    // created by your chat client / agent framework integration.
    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call1", "Stop", default)]));

    ChatConfiguration chatConfiguration = new(_chatClient);
    ToolCallAccuracyEvaluatorContext context = new([.. MotorTools.AsAITools()]);

#pragma warning disable AIEVAL001
    ToolCallAccuracyEvaluator evaluator = new();
    EvaluationResult result = await evaluator.EvaluateAsync(history, response, chatConfiguration, additionalContext: [context]);
    BooleanMetric toolAccuracy = result.Get<BooleanMetric>(ToolCallAccuracyEvaluator.ToolCallAccuracyMetricName);

    Assert.NotNull(toolAccuracy.Interpretation);
    Assert.Null(toolAccuracy.Interpretation!.Reason);
    Assert.True(toolAccuracy.Value);
    Assert.False(toolAccuracy.Interpretation!.Failed);
  }

  [Fact]
  public async Task CoherenceEvaluator_CoherentResponse()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are forward, backward, turn left, turn right, and stop.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, """
      1. turn right (90°)
      2. forward (1 meter)
      3. turn left (90°)
      4. forward (1 meter)
      5. turn left (90°)
      6. forward (1 meter)
      7. turn right (90°)            
      """));

    CoherenceEvaluator coherenceEvaluator = new();
    EvaluationResult result = await coherenceEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient));
    NumericMetric coherence = result.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);

    Assert.NotNull(coherence.Interpretation);
    Assert.Null(coherence.Interpretation!.Reason);
    Assert.True(coherence.Interpretation!.Rating >= EvaluationRating.Good);
    Assert.False(coherence.Interpretation!.Failed);
  }

  [Fact]
  public async Task CoherenceEvaluator_IncoherentResponse()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are forward, backward, turn left, turn right, and stop.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, """
      Turn left because trees are green and I like the color, 
      forward 2 steps but watch for rain clouds, stop if you hear music, 
      backward might work better on Tuesdays when the moon is full, 
      the car needs to turn right eventually but first check your shoes.
      """));

    CoherenceEvaluator coherenceEvaluator = new();
    EvaluationResult result = await coherenceEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient));
    NumericMetric coherence = result.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);

    Assert.NotNull(coherence.Interpretation);
    Assert.NotNull(coherence.Interpretation!.Reason);
    Assert.False(coherence.Interpretation!.Rating >= EvaluationRating.Good);
    Assert.True(coherence.Interpretation!.Failed);
  }

  [Fact]
  public async Task RelevanceEvaluator_RelevantResponse()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are forward, backward, turn left, turn right, and stop.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, """
      1. turn right (90°)
      2. forward (1 meter)
      3. turn left (90°)
      4. forward (1 meter)
      5. turn left (90°)
      6. forward (1 meter)
      7. turn right (90°)            
      """));

    var relevanceEvaluator = new RelevanceEvaluator();
    EvaluationResult result = await relevanceEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient));
    NumericMetric relevance = result.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);

    Assert.NotNull(relevance.Interpretation);
    Assert.NotNull(relevance.Interpretation!.Reason);
    Assert.True(relevance.Interpretation!.Rating >= EvaluationRating.Average);
    Assert.True(relevance.Interpretation!.Failed);
  }

  [Fact]
  public async Task RelevanceEvaluator_IrelevantResponse()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are forward, backward, turn left, turn right, and stop.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, """
      1. forward (1 meter)
      2. stop  
      3. forward (100 meter)
      """));

    RelevanceEvaluator relevanceEvaluator = new();
    EvaluationResult result = await relevanceEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient));
    NumericMetric relevance = result.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);

    Assert.NotNull(relevance.Interpretation);
    Assert.NotNull(relevance.Interpretation!.Reason);
    Assert.False(relevance.Interpretation!.Rating >= EvaluationRating.Good);
    Assert.True(relevance.Interpretation!.Failed);
  }

  [Fact]
  public async Task GroundednessEvaluator_GroundedResponse()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are very basic.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, """
      1. turn right (90°)
      2. forward (1 meter)
      3. turn left (90°)
      4. forward (1 meter)
      5. turn left (90°)
      6. forward (1 meter)
      7. turn right (90°)            
      """));

    GroundednessEvaluatorContext baselineResponseForGroundedness = new("""
      The available robot car permitted moves are forward, backward, turn left, turn right, and stop.
      """);

    GroundednessEvaluator groundednessEvaluator = new();
    EvaluationResult result = await groundednessEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient),
      [baselineResponseForGroundedness]);
    NumericMetric groundedness = result.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);

    Assert.NotNull(groundedness.Interpretation);
    Assert.Null(groundedness.Interpretation!.Reason);
    Assert.True(groundedness.Interpretation!.Rating >= EvaluationRating.Good);
    Assert.False(groundedness.Interpretation!.Failed);
  }

  [Fact]
  public async Task GroundednessEvaluator_UngroundedResponse()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System,"""
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are very basic.
        """),
      new(ChatRole.User,"""
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, """
      1. go round
      2. retreat
      3. jump over the tree
      4. evazive maneveur
      """));

    GroundednessEvaluatorContext baselineResponseForGroundedness = new("""
      The available robot car permitted moves are forward, backward, turn left, turn right, and stop.
      """);
    GroundednessEvaluator groundednessEvaluator = new();
    EvaluationResult result = await groundednessEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient),
      [baselineResponseForGroundedness]);
    NumericMetric groundedness = result.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);

    Assert.NotNull(groundedness.Interpretation);
    Assert.NotNull(groundedness.Interpretation!.Reason);
    Assert.False(groundedness.Interpretation!.Rating >= EvaluationRating.Good);
    Assert.True(groundedness.Interpretation!.Failed);
  }

  [Fact]
  public async Task CompositeEvaluator_ResponseOk()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are very basic.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    ChatResponse response = new(new ChatMessage(ChatRole.Assistant, """
      1. turn right (90°)
      2. forward (1 meter)
      3. turn left (90°)
      4. forward (1 meter)
      5. turn left (90°)
      6. forward (1 meter)
      7. turn right (90°)            
      """));

    CoherenceEvaluator coherenceEvaluator = new();
    EquivalenceEvaluator equivalenceEvaluator = new();
    CompositeEvaluator compositeEvaluator = new(coherenceEvaluator, equivalenceEvaluator);

    var baselineResponseForEquivalence = new EquivalenceEvaluatorContext("""
      1. turn right
      2. forward
      3. turn left
      4. forward
      5. turn left
      6. forward
      7. turn right
      """);

    EvaluationResult result = await compositeEvaluator.EvaluateAsync(history, response, new ChatConfiguration(_chatClient),
      [baselineResponseForEquivalence]);
    NumericMetric coherence = result.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);
    NumericMetric equivalence = result.Get<NumericMetric>(EquivalenceEvaluator.EquivalenceMetricName);

    Assert.NotNull(coherence.Interpretation);
    Assert.Null(coherence.Interpretation!.Reason);
    Assert.True(coherence.Interpretation!.Rating >= EvaluationRating.Good);
    Assert.False(coherence.Interpretation!.Failed);

    Assert.NotNull(equivalence.Interpretation);
    Assert.Null(equivalence.Interpretation!.Reason);
    Assert.True(equivalence.Interpretation!.Rating >= EvaluationRating.Good);
    Assert.False(equivalence.Interpretation!.Failed);
  }

  [Fact]
  public async Task BasicMovesEvaluator_ValidMoves_ReturnsOk()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are very basic.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    // Valid response with only basic moves
    ChatResponse validResponse = new(new ChatMessage(ChatRole.Assistant, """
      1. turn right (90°)
      2. forward (1 meter)
      3. turn left (90°)
      4. forward (1 meter)
      5. stop
      """));

    BasicMovesEvaluator basicMovesEvaluator = new();
    EvaluationResult result = await basicMovesEvaluator.EvaluateAsync(history, validResponse, new ChatConfiguration(_chatClient));
    BooleanMetric basicMoves = result.Get<BooleanMetric>(BasicMovesEvaluator.BasicMovesMetricName);

    Assert.NotNull(basicMoves.Interpretation);
    Assert.False(basicMoves.Interpretation!.Failed); // Should pass - only valid moves
    Assert.True(basicMoves.Value); // Should be true
    Assert.Contains("valid basic moves", basicMoves.Interpretation!.Reason);
  }

  [Fact]
  public async Task BasicMovesEvaluator_InvalidMoves_ReturnsFailed()
  {
    List<ChatMessage> history =
    [
      new(ChatRole.System, """
        You are an AI assistant controlling a robot car.
        The available robot car permitted moves are very basic.
        """),
      new(ChatRole.User, """
        You have to break down the provided complex commands into basic moves you know.
        Respond only with the permitted moves, without any additional explanations.
                
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then come back to the original path."
        """)
    ];

    // Invalid response with non-basic moves
    ChatResponse invalidResponse = new(new ChatMessage(ChatRole.Assistant, """
      1. jump over the obstacle
      2. roll to the side
      3. slide under it
      4. forward (1 meter)
      5. stop
      """));

    BasicMovesEvaluator basicMovesEvaluator = new BasicMovesEvaluator();
    EvaluationResult result = await basicMovesEvaluator.EvaluateAsync(history, invalidResponse, new ChatConfiguration(_chatClient));
    BooleanMetric basicMoves = result.Get<BooleanMetric>(BasicMovesEvaluator.BasicMovesMetricName);

    Assert.NotNull(basicMoves.Interpretation);
    Assert.True(basicMoves.Interpretation!.Failed); // Should fail - contains invalid moves
    Assert.False(basicMoves.Value); // Should be false
    Assert.Contains("invalid moves", basicMoves.Interpretation!.Reason);
    Assert.Contains("jump", basicMoves.Interpretation!.Reason);
  }
}
