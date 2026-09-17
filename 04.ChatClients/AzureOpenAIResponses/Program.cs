using Microsoft.Extensions.Configuration;
using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var endpoint = configuration["AzureOpenAI:Endpoint"]!;
var apiKey = configuration["AzureOpenAI:ApiKey"]!;
var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

var prompt = """
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
Console.WriteLine($"USER: {prompt}");

OpenAIClient client = new (
  new ApiKeyCredential(apiKey), 
  new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

#pragma warning disable OPENAI001
ResponsesClient azureOpenAIChatClient = client.GetResponsesClient();
ClientResult<ResponseResult> response = azureOpenAIChatClient.CreateResponse(deploymentName, prompt);
Console.WriteLine($"\nAssistant (Azure Responses): {response.Value.GetOutputText()}");

// Using IChatClient interface
IChatClient chatClient = client.GetResponsesClient().AsIChatClient(deploymentName);
ChatResponse chatResponse = await chatClient.GetResponseAsync(prompt);

Console.WriteLine($"\nAssistant (IChatClient): {chatResponse.Text}");
