using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

ChatClientAgent motorsAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
    You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    You have to break down the provided complex commands into the basic moves you know.
    Respond only with the moves and their parameters (angle or distance), without any additional explanations.
    """
  );

AgentSession session = await motorsAgent.CreateSessionAsync();

var query = $"""  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then return to the original path."
  """;
ColorHelper.PrintColoredLine($"USER (MOTORS): {query}", ConsoleColor.Yellow);

AgentResponse response = await motorsAgent.RunAsync(query, session);
ColorHelper.PrintColoredLine($"ASSISTANT (MOTORS): {response.Text}", ConsoleColor.Green);

ChatClientAgent auditorAgent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
    You are an AI auditor overseeing a robot car controlled by another AI agent called "MotorsAgent".
    You need to ensure safety and correctness.
    """
  );

var auditQuery = $"""  
  Audit request: 
  "Please explain the reasons for the last moves in less than 50 words."
  """;
ColorHelper.PrintColoredLine($"USER (AUDITOR): {auditQuery}", ConsoleColor.Yellow);

response = await auditorAgent.RunAsync(auditQuery, session);
ColorHelper.PrintColoredLine($"ASSISTANT (AUDITOR): {response.Text}", ConsoleColor.Green);
