using AITools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

// Build a tool lookup for manual invocation
var tools = MotorTools.AsAITools().ToList();
var toolsByName = tools.ToDictionary(t => t.Name);

// UseProvidedChatClientAsIs = true prevents the agent from auto-executing tools
AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
  UseProvidedChatClientAsIs = true,
  Name = "RobotCarAgent",
  Description = "An agent that assists a robot with the basic moves.",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
      You have to break down the provided complex commands into the basic moves you know.
      Respond only with the moves and their parameters (angle or distance), without any additional explanations.
      """,
    Tools = [.. tools],
    AllowMultipleToolCalls = true,
    ToolMode = ChatToolMode.Auto
  }
});

AgentSession session = await agent.CreateSessionAsync();

var query = @"Complex command: ""Go left and right then stop.""";

AgentResponse response = await agent.RunAsync(query, session);

// Manual tool-calling loop: intercept tool calls, invoke them, send results back
while (true)
{
  var functionCalls = response.Messages
    .Where(m => m.Role == ChatRole.Assistant)
    .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
    .ToList();

  if (functionCalls.Count == 0)
  {
    break;
  }

  Console.WriteLine($"TOOL CALLINGS LEFT: {functionCalls.Count}");

  List<ChatMessage> toolResultMessages = [];

  foreach (var functionCall in functionCalls)
  {
    // Look up and invoke the tool manually
    var result = await toolsByName[functionCall.Name].InvokeAsync(new AIFunctionArguments(functionCall.Arguments!));
    Console.WriteLine($"  Tool: {functionCall.Name} => {result}");

    toolResultMessages.Add(new ChatMessage(ChatRole.Tool,
      [new FunctionResultContent(functionCall.CallId, result)]));
  }

  // Send tool results back to the agent for the next round
  response = await agent.RunAsync(toolResultMessages, session);
}

Console.WriteLine($"\nAssistant: {response.Text}");
