using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using System.Data;

IClientTransport httpTransport = new HttpClientTransport(new()
{
  Endpoint = new Uri("http://localhost:3001"),
  Name = "Motors Client",
});

await using var mcpClient = await McpClient.CreateAsync(httpTransport);

// List available MCP tools
IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine("TOOLS AVAILABLE:");
foreach (var tool in mcpTools)
{
  var arguments = tool.JsonSchema.GetProperty("properties").ToString();
  Console.WriteLine($"  {tool} {arguments}");
}
Console.WriteLine();

// List available MCP prompts
IList<McpClientPrompt> mcpPrompts = await mcpClient.ListPromptsAsync();
Console.WriteLine("PROMPTS AVAILABLE:");
foreach (var prompt in mcpPrompts)
{
  var arguments = string.Join(",", System.Text.Json.JsonSerializer.Serialize(prompt.ProtocolPrompt.Arguments));
  Console.WriteLine($"  {prompt.Name} {arguments}");
}
Console.WriteLine();

// List available MCP resources
IList<McpClientResource> mcpResources = await mcpClient.ListResourcesAsync();
Console.WriteLine("RESOURCES AVAILABLE:");
foreach (var resource in mcpResources)
{
  var arguments = string.Join(",", System.Text.Json.JsonSerializer.Serialize(resource.ProtocolResource));
  Console.WriteLine($"  {resource.Name} {arguments}");
}
Console.WriteLine();

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
////var endpoint = configuration["AzureOpenAI:Endpoint"]!;
////var apiKey = configuration["AzureOpenAI:ApiKey"]!;
////var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;
var model = configuration["OpenAI:ModelId"]!;
var apiKey = configuration["OpenAI:ApiKey"]!;

// Initialize Azure OpenAI Chat Client
////IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
////  .GetChatClient(deploymentName)
////  .AsIChatClient();
IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

// Fetch a tool to use its definition
var mcpTool = await mcpClient.CallToolAsync("turn_left",
  arguments: new Dictionary<string, object?> { { "angle", 99 } }
);
var toolResponse = mcpTool.Content.FirstOrDefault() as TextContentBlock;
Console.WriteLine($"TOOL RESPONSE: {toolResponse?.Text}");
Console.WriteLine();

// Fetch prompts and extract user messages
var mcpPrompt = await mcpClient.GetPromptAsync("message_prompt");
var userPrompt = mcpPrompt.Messages.SingleOrDefault(m => m.Role == Role.User)?.Content as TextContentBlock;
var parametrizedMcpPrompt = await mcpClient.GetPromptAsync("parametrized_message_prompt",
  arguments: new Dictionary<string, object?> { { "action", "There is a tree directly in front of the car. Avoid it and then return to the original path." } }
);
var userParametrizedPrompt = parametrizedMcpPrompt.Messages.SingleOrDefault(m => m.Role == Role.User)?.Content as TextContentBlock;
Console.WriteLine($"SIMPLE PROMPT RESPONSE: {userPrompt?.Text}");
Console.WriteLine($"PROMPT TEMPLATE (PARAMETRIZED) RESPONSE: {userParametrizedPrompt?.Text}");
Console.WriteLine();

// Fetch a resource to use its definition
var mcpResource = await mcpClient.ReadResourceAsync("resource://mcp/bio");
var mcpResourceResponse = mcpResource.Contents.FirstOrDefault() as TextResourceContents;
Console.WriteLine($"RESOURCE RESPONSE: {mcpResourceResponse?.Text}");
Console.WriteLine();

// Create AI agent with MCP tools
AIAgent agent = chatClient.AsAIAgent("""
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
  You have to break down the provided complex commands into the basic moves you know. Run the basic moves using the corresponding tools.
  Respond only with the moves and their parameters (angle or distance), without any additional explanations.
  """,
  tools: [.. mcpTools.Cast<AITool>()]
);

var query = userParametrizedPrompt!.Text;

Console.WriteLine("AGENT RESPONSE:");
AgentResponse response = await agent.RunAsync(query);
Console.WriteLine(response);
