using Adapters;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
var embeddingModel = configuration["OpenAI:EmbeddingModelId"];

IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

var embeddingGenerator = new OpenAIClient(apiKey)
  .GetEmbeddingClient(embeddingModel)
  .AsIEmbeddingGenerator();

TextSearchProviderOptions textSearchOptions = new()
{
  SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
  RecentMessageMemoryLimit = 6
};

// Initialize the keyword-based search provider with options
CustomKeywordSearchAdapter.Initialize(textSearchOptions, topResults: 5);

AIContextProvider keywordSearchProvider = new TextSearchProvider(CustomKeywordSearchAdapter.KeywordSearch, textSearchOptions);

ChatClientAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
  Name = "RobotCarAgent",
  Description = "An agent that assists a robot with the basic moves.",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
      You have to break down the provided complex commands into the basic moves you know.
      Respond only with the moves and their parameters (angle or distance), without any additional explanations.
      """
  },
  AIContextProviders = [keywordSearchProvider]
});

AgentSession session = await agent.CreateSessionAsync();

var query = $"""  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then return to the original path."
  """;
ColorHelper.PrintColoredLine($"USER: {query}", ConsoleColor.Yellow);
AgentResponse response = await agent.RunAsync(query, session);
ColorHelper.PrintColoredLine($"ASSISTANT: {response.Text}", ConsoleColor.Green);

var followUpQuery = "What was your second last basic move?";
ColorHelper.PrintColoredLine($"USER: {followUpQuery}", ConsoleColor.Yellow);
AgentResponse followUpResponse = await agent.RunAsync(followUpQuery, session);
ColorHelper.PrintColoredLine($"ASSISTANT: {followUpResponse.Text}", ConsoleColor.Green);

Console.WriteLine("HISTORY:");
await AgentsHelper.PrintChatMessagesAsync(session);
