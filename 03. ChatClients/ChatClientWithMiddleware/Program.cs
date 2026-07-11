using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

var query = """
  ## Persona
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    
  ## Action
  You have to break down the provided complex commands into the basic moves you know.
    
  ## Complex Command
  There is a tree in front of the car. Avoid it and resume the original path.

  ## Template
  Respond with a JSON array like [move1, move2, move3].
  Do not respond with reasoning, comments, or any additional text.
  """;
Console.WriteLine($"USER: {query}");

// Using IChatClient interface
IChatClient baseClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
  builder.AddConsole();
  builder.SetMinimumLevel(LogLevel.Information);
});

IChatClient chatClient = new ChatClientBuilder(baseClient)
  .UseLogging(loggerFactory)
  .Build();

ChatResponse chatResponse = await chatClient.GetResponseAsync(query);
////Console.WriteLine($"\nAssistant: {chatResponse.Text}");
