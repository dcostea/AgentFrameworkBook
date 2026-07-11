using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Connectors.InMemory;
using OpenAI;
using OpenAI.Chat;
using Providers;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

const int TopResults = 5;

VectorStoreChatHistoryProvider vectorStoreChatHistoryProvider = new(new InMemoryVectorStore(), default, topResults: TopResults);

ChatClientAgent agent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent(new ChatClientAgentOptions
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
    ChatHistoryProvider = vectorStoreChatHistoryProvider
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

////ChatClientAgent weatherContextAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
////{
////  Name = "WeatherContextAgent",
////  Description = "An agent that persists the weather context.",
////  ChatOptions = new ChatOptions
////  {
////    Instructions = """
////      You are an AI assistant persisting the weather context.
////      Respond echoing the user provided info, or answering the user query.
////      """
////  },
////  ChatHistoryProvider = vectorStoreChatHistoryProvider
////});

////AgentResponse response1 = await weatherContextAgent.RunAsync("""
////  June 1, 2025 - Morning: 14°C, partly cloudy, wind 8 km/h, dry. Afternoon: 20°C, mostly sunny, wind 12 km/h, no rain. Night: 13°C, clear, wind 6 km/h, calm.
////  """, session);
////AgentResponse response2 = await weatherContextAgent.RunAsync("""
////  June 2, 2025 - Morning: 15°C, sunny, wind 10 km/h, dry. Afternoon: 22°C, mostly sunny, wind 14 km/h, dry roads. Night: 14°C, few clouds, wind 8 km/h, no precipitation.
////  """, session);
////AgentResponse response3 = await weatherContextAgent.RunAsync("""
////  June 3, 2025 - Morning: 13°C, cloudy, wind 10 km/h, dry. Afternoon: 21°C, clearing skies, wind 13 km/h, dry. Night: 13°C, mostly clear, wind 7 km/h, calm.
////  """, session);
////AgentResponse response4 = await weatherContextAgent.RunAsync("""
////  June 4, 2025 - Morning: 12°C, overcast, wind 11 km/h, dry. Afternoon: 19°C, showers likely, wind 16 km/h, wet roads possible. Night: 12°C, cloudy, wind 9 km/h, light drizzle.
////  """, session);
////AgentResponse response6 = await weatherContextAgent.RunAsync("""
////  June 5, 2025 - Morning: 12°C, cloudy, wind 13 km/h, occasional light rain. Afternoon: 18°C, overcast, wind 18 km/h, scattered rain showers. Night: 11°C, mostly cloudy, wind 10 km/h, some drizzle.
////  """, session);
////AgentResponse response7 = await weatherContextAgent.RunAsync("""
////  ##Query: What was the temperature on June 2, 2025, afternoon?
////  """, session);

Console.WriteLine("HISTORY:");
foreach (Microsoft.Extensions.AI.ChatMessage message in vectorStoreChatHistoryProvider.ChatMessages)
{
  var source = message.GetAgentRequestMessageSourceType();
  Console.WriteLine($"[{source.Value}] {message.Role}: ");
  Console.WriteLine($"{message.Text}");
}
