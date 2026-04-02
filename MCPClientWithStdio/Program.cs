using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;

IClientTransport stdioTransport = new StdioClientTransport(new StdioClientTransportOptions
{
  Name = "Motors Client",
  Command = "dotnet",
  Arguments = ["run", "--project", @"..\..\..\..\MCPServerWithStdio\MCPServerWithStdio.csproj"],
});

await using var mcpClient = await McpClient.CreateAsync(stdioTransport);

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
  var arguments = string.Join(",", JsonSerializer.Serialize(prompt.ProtocolPrompt.Arguments));
  Console.WriteLine($"  {prompt.Name} {arguments}");
}
Console.WriteLine();

// List available MCP resources
IList<McpClientResource> mcpResources = await mcpClient.ListResourcesAsync();
Console.WriteLine("RESOURCES AVAILABLE:");
foreach (var resource in mcpResources)
{
  var arguments = string.Join(",", JsonSerializer.Serialize(resource.ProtocolResource));
  Console.WriteLine($"  {resource.Name} {arguments}");
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

var query = userParametrizedPrompt!.Text;

Console.WriteLine("AGENT RESPONSE:");
AgentResponse response = await agent.RunAsync(query);
Console.WriteLine(response);

Console.WriteLine();

// --- TASKS DEMO ---
#pragma warning disable MCPEXP001 // Tasks are experimental in MCP SDK v1.0

// Must be registered BEFORE the call and kept alive until PollTaskUntilCompleteAsync returns.
// The SDK's internal handler (inside CallToolAsTaskAsync) is disposed as soon as the initial
// tools/call response returns (~instant for tasks), before the background task runs.
await using IAsyncDisposable progressReg = mcpClient.RegisterNotificationHandler(
  "notifications/progress",
  (notification, ct) =>
  {
    double prog = notification.Params?["progress"]?.GetValue<double>() ?? 0;
    double? total = notification.Params?["total"]?.GetValue<double>();
    Console.WriteLine($"  PROGRESS: {prog}/{total} motors checked");
    return ValueTask.CompletedTask;
  });

// run_diagnostics is a long-running task.
// Either run_diagnostics or run_diagnostics_with_progress can be used, but not both.
// Remember to comment out one of them.
////McpTask task = await mcpClient.CallToolAsTaskAsync(
////  "run_diagnostics",
////  arguments: new Dictionary<string, object?> { { "detailed", true } });
////Console.WriteLine($"TASK STARTED: {task.TaskId} | Status: {task.Status}");

// run_diagnostics_with_progress is a long-running task that sends progress updates.
// Either run_diagnostics or run_diagnostics_with_progress can be used, but not both.
// Remember to comment out one of them.
McpTask task = await mcpClient.CallToolAsTaskAsync(
  "run_diagnostics_with_progress",
  arguments: new Dictionary<string, object?> { { "detailed", true } },
  progress: new Progress<ProgressNotificationValue>(_ => { }));
Console.WriteLine($"TASK STARTED: {task.TaskId} | Status: {task.Status}");

// Poll until Completed / Failed / Cancelled, then retrieve the result
McpTask completed = await mcpClient.PollTaskUntilCompleteAsync(task.TaskId);
JsonElement result = await mcpClient.GetTaskResultAsync(task.TaskId);
Console.WriteLine($"TASK DONE: {completed.Status} | RESULT: {result}");
