using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder()
  .AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
var chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model);

AIAgent agent = chatClient.AsAIAgent("""
  You are an AI assistant controlling a robot car capable of performing 
  basic moves: forward, backward, turn left, turn right, and stop.
  You have to break down the provided complex commands into the basic 
  moves you know.
  Respond only with the moves, without any additional explanations.
  Execute the corresponding basic moves using the available tools.
  """,
  tools: [.. MotorTools.AsAITools()]
);

AgentSession session = await agent.CreateSessionAsync();

while (true)
{
  Console.Write("User: ");
  var input = Console.ReadLine();
  if (string.IsNullOrEmpty(input)) break;

  var prompt = $"""  
    Complex command: 
    "{input}"
    """;

  AgentResponse response = await agent.RunAsync(prompt, session);
  Console.WriteLine($"Assistant: {response.Text}");
}
