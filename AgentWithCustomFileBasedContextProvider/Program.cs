using Providers;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

var providerInstructionsFilePath = @"Data\robot-car-guidelines.txt";
var providerChatHistoryFilePath = @"Data\robot-car-chat-history.json";

AIContextProvider fileBasedContextProvider = new CustomFileBasedContextProvider(providerInstructionsFilePath, providerChatHistoryFilePath);

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
  AIContextProviders = [fileBasedContextProvider]
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

var anotherFollowUpQuery = "What is the wind speed later afternoon?";
ColorHelper.PrintColoredLine($"USER: {anotherFollowUpQuery}", ConsoleColor.Yellow);
AgentResponse anotherFollowUpResponse = await agent.RunAsync(anotherFollowUpQuery, session);
ColorHelper.PrintColoredLine($"ASSISTANT: {anotherFollowUpResponse.Text}", ConsoleColor.Green);

Console.WriteLine("HISTORY:");
await AgentsHelper.PrintChatMessagesAsync(session);
