using AITools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
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
      """,
      Tools = [.. MotorTools.AsAITools()],
      ToolMode = ChatToolMode.Auto,
      //ToolMode = ChatToolMode.None,
      //ToolMode = ChatToolMode.RequireAny,
      //ToolMode = ChatToolMode.RequireSpecific("BackwardAsync"),
    }
  });

var firstPrompt = "Go left and right then stop.";
Console.WriteLine($"USER: {firstPrompt}");
AgentResponse firstResponse = await agent.RunAsync(firstPrompt);
Console.WriteLine("RESPONSE:");
Console.WriteLine(firstResponse.Text);

Console.WriteLine();

var secondPrompt = "What movements can you perform?";
Console.WriteLine($"USER: {secondPrompt}");
AgentResponse secondResponse = await agent.RunAsync(secondPrompt);
Console.WriteLine("RESPONSE:");
Console.WriteLine(secondResponse.Text);
