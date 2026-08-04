using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using OpenAI.Chat;
using System.Data;
using System.Text.Json;

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
foreach (var mcpPrompt in mcpPrompts)
{
  var arguments = string.Join(",", JsonSerializer.Serialize(mcpPrompt.ProtocolPrompt.Arguments));
  Console.WriteLine($"  {mcpPrompt.Name} {arguments}");
}
Console.WriteLine();

// List available MCP resources
IList<McpClientResource> mcpResources = await mcpClient.ListResourcesAsync();
Console.WriteLine("RESOURCES AVAILABLE:");
foreach (var mcpResource in mcpResources)
{
  var arguments = string.Join(",", JsonSerializer.Serialize(mcpResource.ProtocolResource));
  Console.WriteLine($"  {mcpResource.Name} {arguments}");
}
Console.WriteLine();

// List available MCP resource templates
IList<McpClientResourceTemplate> mcpResourceTemplates = await mcpClient.ListResourceTemplatesAsync();
Console.WriteLine("RESOURCE TEMPLATES AVAILABLE:");
foreach (var mcpResourceTemplate in mcpResourceTemplates)
{
  var details = JsonSerializer.Serialize(mcpResourceTemplate.ProtocolResourceTemplate);
  Console.WriteLine($"  {mcpResourceTemplate.Name} {details}");
}
Console.WriteLine();

// Fetch a tool to use its definition
var mcpTool = await mcpClient.CallToolAsync("turn_left",
  arguments: new Dictionary<string, object?> { { "angle", 99 } }
);
var toolResponse = mcpTool.Content.FirstOrDefault() as TextContentBlock;
Console.WriteLine($"TOOL RESPONSE: {toolResponse?.Text}");
Console.WriteLine();

// Fetch prompts and extract user messages
var mcpClientPrompt = await mcpClient.GetPromptAsync("message_prompt");
var userPrompt = mcpClientPrompt.Messages.SingleOrDefault(m => m.Role == Role.User)?.Content as TextContentBlock;
var parametrizedMcpClientPrompt = await mcpClient.GetPromptAsync("parametrized_message_prompt",
  arguments: new Dictionary<string, object?> { { "action", "There is a tree directly in front of the car. Avoid it and then return to the original path." } }
);
var userParametrizedPrompt = parametrizedMcpClientPrompt.Messages.SingleOrDefault(m => m.Role == Role.User)?.Content as TextContentBlock;
Console.WriteLine($"SIMPLE PROMPT RESPONSE: {userPrompt?.Text}");
Console.WriteLine($"PROMPT TEMPLATE (PARAMETRIZED) RESPONSE: {userParametrizedPrompt?.Text}");
Console.WriteLine();

// Fetch a static resource
var mcpClientResource = await mcpClient.ReadResourceAsync("resource://mcp/bio");
var mcpResourceResponse = mcpClientResource.Contents.FirstOrDefault() as TextResourceContents;
Console.WriteLine($"RESOURCE RESPONSE: {mcpResourceResponse?.Text}");

// Fetch a template resource with a concrete name
var mcpGreetClientResource = await mcpClient.ReadResourceAsync("resource://mcp/greet/Robby");
var mcpGreetResourceResponse = mcpGreetClientResource.Contents.FirstOrDefault() as TextResourceContents;
Console.WriteLine($"TEMPLATE RESOURCE RESPONSE: {mcpGreetResourceResponse?.Text}");
Console.WriteLine();

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

// Create AI agent with MCP tools
ChatClientAgent agent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent("""
    You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    You have to break down the provided complex commands into the basic moves you know. Run the basic moves using the corresponding tools.
    Respond only with the moves and their parameters (angle or distance), without any additional explanations.
    """,
    tools: [.. mcpTools.Cast<AITool>()]
  );

var prompt = userParametrizedPrompt!.Text;

Console.WriteLine("AGENT RESPONSE:");
AgentResponse response = await agent.RunAsync(prompt);
Console.WriteLine(response);
