using Microsoft.Extensions.Configuration;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.AI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
var chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient();

ChatClientAgent agent = chatClient.AsAIAgent(instructions: """
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
  You have to break down the provided complex commands into the basic moves you know.
  Use a JSON array like [move1, move2, move3] for the response.
  Respond only with the moves and their parameters (angle or distance), without any additional explanations.
  """,
  name: "RobotCarAgent",
  description: "An AI agent that controls a robot car."
);

var query = """  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then return to the original path."
  """;
AgentResponse response = await agent.RunAsync(query);
Console.WriteLine(response.Text);

var followupQuery = """  
  Complex command: 
  "What was your second last move?"
  """;
AgentResponse followupResponse = await agent.RunAsync(followupQuery);
Console.WriteLine(followupResponse.Text);
