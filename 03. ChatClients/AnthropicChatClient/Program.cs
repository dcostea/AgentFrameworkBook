using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["Anthropic:ModelId"];
var apiKey = configuration["Anthropic:ApiKey"];

var query = """
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
  You have to break down the provided complex commands into the basic moves you know.
  There is a tree in front of the car. Avoid it and resume the original path.

  Respond with a JSON array like [move1, move2, move3].
  Do not respond with reasoning, comments, or any additional text.
  """;
Console.WriteLine($"USER: {query}");

// Using IChatClient interface
IChatClient chatClient = new AnthropicClient() { ApiKey = apiKey }
  .AsIChatClient(defaultModelId: model);

ChatResponse chatResponse = await chatClient.GetResponseAsync(query);
Console.WriteLine($"\nAssistant (IChatClient): {chatResponse.Text}");

