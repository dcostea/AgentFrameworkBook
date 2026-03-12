using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

ChatClientAgent agent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsAIAgent(instructions: """
    You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    You have to break down the provided complex commands into the basic moves you know.
    Use a JSON array like [move1, move2, move3] for the response.
    Respond only with the moves and their parameters (angle or distance), without any additional explanations.
    """
  );

var query = """  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then return to the original path."
  """;

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(query))
{
  Console.Write(update.Text);

  if (update.Contents.FirstOrDefault() is UsageContent usageContent)
  {
    Console.WriteLine($"\n\nInput Tokens: {usageContent.Details.InputTokenCount}");
    Console.WriteLine($"Output Tokens: {usageContent.Details.OutputTokenCount}");
    Console.WriteLine($"Total Tokens: {usageContent.Details.TotalTokenCount}");
  }
}
Console.WriteLine();
