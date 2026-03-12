using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.ClientModel;

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

  // warning: reasoning models don't support all attributes below
  ChatOptions options = new()
  {
    Temperature = 0.4F,    // Creativity level (0.0-2.0)
    MaxOutputTokens = 5,    // Maximum response length
    TopP = 0.9f,    // Nucleus sampling
    FrequencyPenalty = 0.5f,    // Reduce repetition
    PresencePenalty = 0.3f,    // Encourage topic diversity
    StopSequences = ["END"],    // Stop generation at specific tokens 
    ResponseFormat = ChatResponseFormat.ForJsonSchema<StepsResponse>(),
  };

  ChatResponse response = await chatClient.GetResponseAsync(conversation, options);
  Console.WriteLine($"\nAssistant: {response.Text}");
  conversation.AddRange(response.Messages);
}

record StepsResponse(string[] Steps);