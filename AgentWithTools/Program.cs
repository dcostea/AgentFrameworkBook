using AITools;
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

//AllowMultipleToolCalls = true to comply with the ReAct pattern

ChatClientAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
  Name = "RobotCarAgent",
  Description = "An agent that assists a robot with the basic moves.",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
      You have to break down the provided complex commands into the basic moves you know.

      Use ReAct: Think → Act (one move) → Observe result → Think → Act...

      Respond only with the moves and their parameters (angle or distance), without any additional explanations.
      """,
    Tools = [.. MotorTools.AsAITools()],
  }
});

var query = $"""  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then return to the original path."
  """;
ColorHelper.PrintColoredLine($"USER: {query}", ConsoleColor.Yellow);
AgentResponse response = await agent.RunAsync(query);
ColorHelper.PrintColoredLine($"ASSISTANT: {response.Text}", ConsoleColor.Green);
