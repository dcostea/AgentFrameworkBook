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
  ## Persona
  You are an AI assistant controlling a robot car capable of performing 
  basic moves: forward, backward, turn left, turn right, and stop.
    
  ## Action
  You have to break down the provided complex commands into the basic 
  moves you know.
    
  ## Template
  Use a JSON array like [move1, move2, move3] for the response.
  Respond only with the moves, without any additional explanations.
  """);

AgentSession session = await agent.CreateSessionAsync();

var prompt1 = "go 10 meters forward then turn back";
Console.WriteLine($"User: {prompt1}");
var response1 = await agent.RunAsync(prompt1, session);
Console.WriteLine($"Assistant: {response1.Text}");

// uncomment the following lines to see how to stream the response from the agent
Console.Write("Assistant: ");
////await foreach (AgentResponseUpdate update in agent
////  .RunStreamingAsync(prompt1, session))
////{
////  Console.Write(update.Text);
////}
////Console.WriteLine();

var prompt2 = "repeat the last maneuvers";
Console.WriteLine($"User: {prompt2}");
var response2 = await agent.RunAsync(prompt2, session);
Console.WriteLine($"Assistant: {response2.Text}");

Console.WriteLine("\nHistory:");

if (session.TryGetInMemoryChatHistory(out var history))
{
  foreach (var message in history)
  {
    Console.WriteLine($"{message.Role}: {message.Text}");
  }
}
