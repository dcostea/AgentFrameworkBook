using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.Text.Json;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

#pragma warning disable MEAI001
ChatClientAgent agent = chatClient.AsAIAgent(instructions: """
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

// Collect every pending approval request from the agent's response
var approvalRequests = response.Messages
  .SelectMany(m => m.Contents)
  .OfType<FunctionApprovalRequestContent>();

// For each request, ask the human operator to approve or deny
var userInputMessages = approvalRequests
  .Select(request =>
  {
    bool isApproved = PromptForApproval(request);
    List<AIContent> contents = [request.CreateResponse(isApproved)];
    return new ChatMessage(ChatRole.User, contents);
  })
  .ToList();

// Send all approval responses back to the agent, this may generate new requests
response = await agent.RunAsync(userInputMessages, session);

// Display the final response from the agent
Console.WriteLine($"\nRESPONSE: {response}");


// Displays the tool call details and waits for the operator to press Y or Enter
bool PromptForApproval(FunctionApprovalRequestContent request)
{
  var toolName = request.FunctionCall.Name;
  var toolArgs = JsonSerializer.Serialize(request.FunctionCall.Arguments);

  Console.WriteLine($"Agent invoking {toolName} {toolArgs}. Approve? [Y/n] ");
  ConsoleKeyInfo key = Console.ReadKey(true);

  bool approved = key.Key is ConsoleKey.Y or ConsoleKey.Enter;
  Console.WriteLine($"AI Tool '{toolName}': {(approved ? "Approved" : "Denied")}");

  return approved;
}
