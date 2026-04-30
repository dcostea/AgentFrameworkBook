using AITools;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Middleware;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

ChatClientAgent motorsAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
  Name = "MotorsAgent",
  Description = "Controls robot car movements.",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      You are the MotorsAgent controlling a robot car.
      Break down complex movement commands into basic moves: forward, backward, turn left, turn right, stop.
      Respond with the sequence of moves needed to accomplish the task.
      """,
    Tools = [.. MotorTools.AsAITools()],
  }
});

AgentSession session = await motorsAgent.CreateSessionAsync();

ColorHelper.PrintColoredLine("""
  ===== TEST 1: SharedFunction Middleware (RemoveEmail) ===== 
  (Without middleware: The email was NOT redacted — sent directly to the LLM provider, GDPR violation)
  (With middleware: The email was redacted — GDPR compliance)
  """);
var query1 = "Navigate to original position. Contact me at john.doe@example.com for updates.";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
AgentResponse result1 = await motorsAgent.RunAsync(query1, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);

ColorHelper.PrintColoredLine("""
  ===== TEST 2: FunctionCalling Middleware (ConstrainDistance) =====
  (Without middleware: Backward ran the full 10 m — no distance constraint, the robot could hit a wall)
  (With middleware: Backward constrained to 5 m max — safer default for obstacle avoidance)
  """);
var query2 = "Move forward 10 meters then go backward 10 meters";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
AgentResponse result2 = await motorsAgent.RunAsync(query2, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);

ColorHelper.PrintColoredLine("""
  ===== TEST 3: Response Middleware (EnforceTokenBudget) ===== 
  (Without middleware: No token budget enforced — costs accumulate silently)
  (With middleware: Token budget enforced — costs controlled)
  """);
var query3 = "Move forward 3 meters, turn right 90 degrees, move forward 3 meters";
ColorHelper.PrintColoredLine($"QUERY: {query3}", ConsoleColor.Yellow);
AgentResponse result3 = await motorsAgent.RunAsync(query3, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result3}\n", ConsoleColor.Yellow);
