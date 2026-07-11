using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Conversations;
using OpenAI.Responses;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

OpenAIClient openAIClient = new(apiKey);

#pragma warning disable OPENAI001
IChatClient chatClient = openAIClient.GetResponsesClient().AsIChatClient(model);

// Create a new conversation (in server-side storage)
ConversationClient conversationClient = openAIClient.GetConversationClient();
string conversationId = await ConversationsHelper.CreateAndGetConversationIdAsync(conversationClient);

ChatClientAgent agent = chatClient.AsAIAgent("""
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
  You have to break down the provided complex commands into the basic moves you know.
  Respond only with the moves and their parameters (angle or distance), without any additional explanations.
  """
);

AgentSession session = await agent.CreateSessionAsync(conversationId);

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
await ConversationsHelper.PrintConversationAsync(conversationClient, conversationId);

// Clean up the conversation
await ConversationsHelper.DeleteConversationAsync(conversationClient, conversationId);
