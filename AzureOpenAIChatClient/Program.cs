using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var endpoint = configuration["AzureOpenAI:Endpoint"]!;
var apiKey = configuration["AzureOpenAI:ApiKey"]!;
var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

var query = """
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
  You have to break down the provided complex commands into the basic moves you know.
  There is a tree in front of the car. Avoid it and resume the original path.
  Respond with a JSON array like [move1, move2, move3].
  Do not respond with reasoning, comments, or any additional text.
  """;
Console.WriteLine($"USER: {query}");

// Using OpenAIClient directly
ChatClient azureOpenAIChatClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
  .GetChatClient(deploymentName);

OpenAI.Chat.UserChatMessage message = new(query);
List<OpenAI.Chat.UserChatMessage> conversation = [];
conversation.Add(message);
////ClientResult<ChatCompletion> response = azureOpenAIChatClient.CompleteChat(conversation);
////Console.WriteLine($"\nAssistant (Azure ChatClient): {response.Value.Content.First().Text}");

// Using IChatClient interface
IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
  .GetChatClient(deploymentName)
  .AsIChatClient();
ChatResponse chatResponse = await chatClient.GetResponseAsync(query);
Console.WriteLine($"\nAssistant (IChatClient): {chatResponse.Text}");
