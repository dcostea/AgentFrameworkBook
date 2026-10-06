using System.Text.Json;
using AITools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using Skills;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"]
  ?? throw new InvalidOperationException("Set OpenAI:ModelId with dotnet user-secrets before running this sample.");
var apiKey = configuration["OpenAI:ApiKey"]
  ?? throw new InvalidOperationException("Set OpenAI:ApiKey with dotnet user-secrets before running this sample.");

using var chatClient = new OpenAIClient(apiKey).GetChatClient(model).AsIChatClient();
using var skillsProvider = RobotCarSkills.CreateProvider();

AIAgent agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
  Name = "RobotCarAgent",
  ChatOptions = new ChatOptions
  {
    Instructions = """
      ## PERSONA
      You are RobotCarAgent, controlling Robby in a local simulation.
      You replace EnvironmentAgent and its three specialists with one agent using skills.

      ## ACTIONS
      1. Read the temperature and droplet level once using the sensor tools. Do not invent readings.
      2. Select a skill using its advertised description and those readings:
         - Temperature above 60 degrees Celsius: load fire-response, even if it is also raining.
         - Otherwise droplet level High: load rain-response.
         - Otherwise: load movement for the user's exploration mission.
      3. Follow the loaded skill's instructions, including loading movement after handling a hazard.
      4. Load each skill before using its resources or scripts. Use the exact advertised names.
      5. Wait for tool results before choosing the next action. Never claim unexecuted actions.
      6. If a tool fails or a reading is unavailable, load movement, run Stop, and report the problem.

      ## TEMPLATE
      End with a short summary of the readings, skills used, and confirmed actions.
      """,
    Tools = [.. SensorTools.AsAITools()],
    AllowMultipleToolCalls = false,
    MaxOutputTokens = 2048
  },
  AIContextProviders = [skillsProvider]
})
  .AsBuilder()
  .Use(async (AIAgent _, FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken) =>
  {
    Console.WriteLine($"[TOOL] {context.Function.Name} {JsonSerializer.Serialize(context.Arguments)}");
    var result = await next(context, cancellationToken);
    if (context.Function.Name is not AgentSkillsProvider.LoadSkillToolName
      and not AgentSkillsProvider.ReadSkillResourceToolName)
    {
      Console.WriteLine($"[RESULT] {result}");
    }
    return result;
  })
  .Build();

var prompt = """
  # MISSION COMMAND: Exploration Trip

  There is a tree directly in front of the car. Avoid it and then come back to the original path.
  The distance to the tree is 50 meters.
  """;

Console.WriteLine("One agent; three model-selected skills; shared mock AITools. No handoff workflow.");
Console.WriteLine($"USER: {prompt}");
var session = await agent.CreateSessionAsync();
var response = await agent.RunAsync(prompt, session);
Console.WriteLine($"ASSISTANT: {response.Text}");
