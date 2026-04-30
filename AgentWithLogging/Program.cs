using AITools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Verbose()
  //.MinimumLevel.Debug()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
  .CreateLogger();

ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(Log.Logger));

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient().AsBuilder()
  .UseLogging(loggerFactory)
  .Build();

AIAgent agent = new OpenAIClient(apiKey)
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
  }
})
  .AsBuilder()
  .UseLogging(loggerFactory)
  .Build();

Console.WriteLine(await agent.RunAsync("go round."));


if (agent is LoggingAgent la) 
{ 
  Console.WriteLine(la.Name);
}
