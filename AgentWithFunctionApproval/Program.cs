using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

ChatClientAgent agent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent(instructions: """
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
  You have to break down the provided complex commands into the basic moves you know.
  """,
  name: "RobotCarAgent",
  tools: [
    // Backward AI Tool requires approval
    new ApprovalRequiredAIFunction(AIFunctionFactory.Create(AITools.MotorTools.BackwardAsync, name: "backward")),
    AIFunctionFactory.Create(AITools.MotorTools.ForwardAsync, name: "forward"),
    AIFunctionFactory.Create(AITools.MotorTools.TurnLeftAsync, name: "turn_left"),
    AIFunctionFactory.Create(AITools.MotorTools.TurnRightAsync, name: "turn_right"),
    // Stop AI Tool requires approval
    new ApprovalRequiredAIFunction(AIFunctionFactory.Create(AITools.MotorTools.StopAsync, name: "stop"))
  ]
);

var query = "Complex command: Danger ahead! Stop! Full back!";

// Create a new conversation session and send the initial command
AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync(query, session);

// Loop until the agent has no more pending approval requests
List<ToolApprovalRequestContent> approvalRequests = GetToolApprovalRequests(response);
while (approvalRequests.Count > 0)
{
  var approvalResponses = approvalRequests
    .Select(request => new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, [request.CreateResponse(PromptForToolApproval(request))]))
    .ToList();

  response = await agent.RunAsync(approvalResponses, session);
  approvalRequests = GetToolApprovalRequests(response);
}

// Display the final response from the agent
Console.WriteLine($"\nRESPONSE: {response}");


static List<ToolApprovalRequestContent> GetToolApprovalRequests(AgentResponse response)
{
  return [.. response.Messages
    .SelectMany(m => m.Contents)
    .OfType<ToolApprovalRequestContent>()];
}

// Displays the tool call details and waits for the operator to press Y or Enter
static bool PromptForToolApproval(ToolApprovalRequestContent request)
{
  var call = request.ToolCall as FunctionCallContent;
  var toolName = call?.Name;
  var toolArgs = JsonSerializer.Serialize(call?.Arguments);

  Console.WriteLine($"Agent invoking {toolName} {toolArgs}. Approve? [Y/n] ");
  ConsoleKeyInfo key = Console.ReadKey(true);

  bool approved = key.Key is ConsoleKey.Y or ConsoleKey.Enter;
  Console.WriteLine($"AI Tool '{toolName}': {(approved ? "Approved" : "Denied")}");

  return approved;
}
