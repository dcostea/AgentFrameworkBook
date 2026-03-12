using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

ChatClientAgent agent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent(instructions: """
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
  You have to break down the provided complex commands into the basic moves you know.
  Respond only with the moves and their parameters (angle or distance), without any additional explanations.
  """,
  "MotorsAgent"
);

AgentSession session = await agent.CreateSessionAsync();

var query = $"""  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then return to the original path."
  """;
ColorHelper.PrintColoredLine($"USER: {query}", ConsoleColor.Yellow);
AgentResponse response = await agent.RunAsync(query, session);
ColorHelper.PrintColoredLine($"ASSISTANT: {response.Text}", ConsoleColor.Green);

Console.WriteLine("\nSerializing session...");
JsonElement serializedSession = await agent.SerializeSessionAsync(session);
string filePath = "agent_session.json";
await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(serializedSession));

await Task.Delay(2000);

Console.WriteLine("\nDeserializing session...");
string reloadedJsonContent = await File.ReadAllTextAsync(filePath);
JsonElement reloadedJson = JsonElement.Parse(reloadedJsonContent);
AgentSession reloadedSession = await agent.DeserializeSessionAsync(reloadedJson);

var followUpQuery = "What was your second last basic move?";
ColorHelper.PrintColoredLine($"USER: {followUpQuery}", ConsoleColor.Yellow);
AgentResponse followUpResponse = await agent.RunAsync(followUpQuery, reloadedSession);
ColorHelper.PrintColoredLine($"ASSISTANT: {followUpResponse.Text}", ConsoleColor.Green);

