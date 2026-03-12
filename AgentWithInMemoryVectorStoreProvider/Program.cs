using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using OpenAI;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
var embeddingModel = configuration["OpenAI:EmbeddingModelId"];

ChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model);

IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = new OpenAIClient(apiKey)
  .GetEmbeddingClient(embeddingModel)
  .AsIEmbeddingGenerator();

VectorStore vectorStore = new InMemoryVectorStore(new InMemoryVectorStoreOptions
{
  EmbeddingGenerator = embeddingGenerator
});

ChatHistoryMemoryProvider chatHistoryMemoryProvider = new(
  vectorStore,
  collectionName: "RobotCarKnowledge",
  vectorDimensions: 1536,
  session => new ChatHistoryMemoryProvider.State(
    storageScope: new() { UserId = "User_1", SessionId = Guid.NewGuid().ToString() },
    searchScope: new() { UserId = "User_1" })
);

ChatClientAgent weatherAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
  Name = "WeatherAgent",
  Description = "An agent that feeds weather information.",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      You are an AI assistant collecting weather information.
      Respond with echo of collected information.
      """
  },
  AIContextProviders = [chatHistoryMemoryProvider],
});

Microsoft.Extensions.AI.ChatMessage weatherReportMessage = new(ChatRole.User, """
  ## Weather Report
  June 1, 2025 - Morning: 14°C, partly cloudy, wind 8 km/h, dry. Afternoon: 20°C, mostly sunny, wind 12 km/h, no rain. Night: 13°C, clear, wind 6 km/h, calm.",
  June 2, 2025 - Morning: 15°C, sunny, wind 10 km/h, dry. Afternoon: 22°C, mostly sunny, wind 14 km/h, dry roads. Night: 14°C, few clouds, wind 8 km/h, no precipitation.",
  June 3, 2025 - Morning: 13°C, cloudy, wind 10 km/h, dry. Afternoon: 21°C, clearing skies, wind 13 km/h, dry. Night: 13°C, mostly clear, wind 7 km/h, calm.",
  June 4, 2025 - Morning: 12°C, overcast, wind 11 km/h, dry. Afternoon: 19°C, showers likely, wind 16 km/h, wet roads possible. Night: 12°C, cloudy, wind 9 km/h, light drizzle.",
  June 5, 2025 - Morning: 12°C, cloudy, wind 13 km/h, occasional light rain. Afternoon: 18°C, overcast, wind 18 km/h, scattered rain showers. Night: 11°C, mostly cloudy, wind 10 km/h, some drizzle.",
  """)
{
  MessageId = Guid.NewGuid().ToString(),
  AuthorName = "System"
};

AgentSession session = await weatherAgent.CreateSessionAsync();

ColorHelper.PrintColoredLine($"USER: {weatherReportMessage}", ConsoleColor.Yellow);
AgentResponse weatherResponse = await weatherAgent.RunAsync(weatherReportMessage, session);
ColorHelper.PrintColoredLine($"ASSISTANT: {weatherResponse.Text}", ConsoleColor.Green);

ChatClientAgent motorsAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
  Name = "MotorsAgent",
  Description = "An agent that assists a robot with the basic moves.",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
      You have to break down the provided complex commands into the basic moves you know.
      Respond only with the moves and their parameters (angle or distance), without any additional explanations.
      """
  },
  AIContextProviders = [chatHistoryMemoryProvider],
});

var query = $"""  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then return to the original path."
  """;
ColorHelper.PrintColoredLine($"USER: {query}", ConsoleColor.Yellow);
AgentResponse response = await motorsAgent.RunAsync(query, session);
ColorHelper.PrintColoredLine($"ASSISTANT: {response.Text}", ConsoleColor.Green);

var followUpQuery = "What was your second last basic move?";
ColorHelper.PrintColoredLine($"USER: {followUpQuery}", ConsoleColor.Yellow);
AgentResponse followUpResponse = await motorsAgent.RunAsync(followUpQuery, session);
ColorHelper.PrintColoredLine($"ASSISTANT: {followUpResponse.Text}", ConsoleColor.Green);

var anotherFollowUpQuery = "Today is 2nd of June, 2PM. What is the temperature?";
ColorHelper.PrintColoredLine($"USER: {anotherFollowUpQuery}", ConsoleColor.Yellow);
AgentResponse anotherFollowUpResponse = await motorsAgent.RunAsync(anotherFollowUpQuery, session);
ColorHelper.PrintColoredLine($"ASSISTANT: {anotherFollowUpResponse.Text}", ConsoleColor.Green);

Console.WriteLine("HISTORY:");
await AgentsHelper.PrintChatMessagesAsync(session);