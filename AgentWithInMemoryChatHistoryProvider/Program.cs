using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

//InMemoryChatHistoryProvider inMemoryChatHistoryProvider = new();

#pragma warning disable MEAI001
MessageCountingChatReducer chatReducer = new(3);
InMemoryChatHistoryProvider inMemoryChatHistoryProvider = new(new InMemoryChatHistoryProviderOptions { ChatReducer = chatReducer });

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
      """,
  },
  ChatHistoryProvider = inMemoryChatHistoryProvider
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

////AgentResponse response1 = await agent.RunAsync("go left 10 degrees", session);
////AgentResponse response2 = await agent.RunAsync("go back 20 meters", session);
////AgentResponse response3 = await agent.RunAsync("turn right 30 degrees", session);
////AgentResponse response4 = await agent.RunAsync("go forward 99 meters", session);
////AgentResponse response5 = await agent.RunAsync("what was the first (earliest) move you can remember?", session);

Console.WriteLine("HISTORY:");
//AgentsHelper.PrintAgentSessionType(session);
await AgentsHelper.PrintChatMessagesAsync(session);