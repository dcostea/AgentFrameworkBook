using AITools;
using Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var endpoint = configuration["AzureOpenAI:Endpoint"];
var apiKey = configuration["AzureOpenAI:ApiKey"];

var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

var systemMessage = """
  ### Persona  
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.  

  ### Action  
  You have to break down the provided complex commands into basic moves you know.  

  ### Context  
  Complex command: "There is a tree directly in front of the car. Avoid it and then come back to the original path."  

  ### Template  
  Respond only with the permitted moves, without any additional explanations.  
  """;

var userMessage = """  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then come back to the original path."  
  """;

var modelName = "ministral-3";
var ollamaServer = "http://localhost:11434";

IChatClient ollamaApiClient = new OllamaApiClient(new Uri(ollamaServer));
////var chatClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
////  //.GetChatClient(deploymentName)
////  .GetOpenAIResponseClient(deploymentName)
var chatClient = ollamaApiClient
  .AsBuilder()
  .UseFunctionInvocation()
  .Build();

List<ChatMessage> messages = [
  new(ChatRole.System, systemMessage),
  new(ChatRole.User, userMessage)
];

var options = new ChatOptions
{
  MaxOutputTokens = 1000,
  ToolMode = ChatToolMode.Auto,
  ModelId = modelName,
  Tools = [.. MotorTools.AsAITools()],
};

ChatResponse response = await chatClient.GetResponseAsync(messages, options);
messages.AddRange(response.Messages);

Console.WriteLine(response);

AgentsHelper.PrintTools(messages);