using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.ClientModel;
using System.Text;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var endpoint = configuration["AzureOpenAI:Endpoint"]!;
var apiKey = configuration["AzureOpenAI:ApiKey"]!;
var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

IChatClient chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new ApiKeyCredential(apiKey))
  .GetChatClient(deploymentName)
  .AsIChatClient();

var system = """
  ## Persona
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    
  ## Action
  You have to break down the provided complex commands into the basic moves you know.

  ## Template
  Respond with a JSON array like [move1, move2, move3].
  Do not respond with reasoning, comments, or any additional text.
  """;

StringBuilder response = new();

List<ChatMessage> conversation = [
  new ChatMessage(ChatRole.System, system)
];
Console.WriteLine($"System:\n{system}\n");

while (true)
{
  Console.Write("User: ");
  var input = Console.ReadLine();
  if (string.IsNullOrEmpty(input)) break;
  var query = $"""  
    ## Context
    Complex command: 
    "{input}"
    """;

  conversation.Add(new ChatMessage(ChatRole.User, query));

  ChatOptions options = new()
  {
    Temperature = 0.4F,
    ResponseFormat = ChatResponseFormat.ForJsonSchema<StepsResponse>(),
  };

  Console.Write($"\nAssistant (Azure ChatClient): ");
  await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(conversation, options))
  {
    response.Append(update);
    Console.Write(update);
  }
  Console.WriteLine();
  conversation.Add(new ChatMessage(ChatRole.Assistant, response.ToString()));
  response.Clear();
}

record StepsResponse(string[] Steps);