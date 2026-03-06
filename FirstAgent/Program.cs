using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
//#pragma warning disable OPENAI001
var chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model);
//.GetResponsesClient(model);
//.AsIChatClient();

AIAgent agent = chatClient.AsAIAgent("""
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
  You have to break down the provided complex commands into basic moves you know.
  """
);

var query = """  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then come back to the original path."  
  """;
var result = await agent.RunAsync(query);
Console.WriteLine(result.Text);
