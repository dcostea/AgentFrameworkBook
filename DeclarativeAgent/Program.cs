using AITools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
var chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model) 
  .AsIChatClient();

var yamlFilePath = @"Agents/MotorsAgent.yaml";
var yamlAgent = await File.ReadAllTextAsync(yamlFilePath);

// Create the agent from the YAML definition.
var agentFactory = new ChatClientPromptAgentFactory(chatClient, [AIFunctionFactory.Create(MotorTools.BackwardAsync, "backward")]);
var agent = await agentFactory.CreateFromYamlAsync(yamlAgent);

var query = """  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then return to the original path."
  """;
AgentResponse response = await agent.RunAsync(query);
Console.WriteLine(response.Text);

