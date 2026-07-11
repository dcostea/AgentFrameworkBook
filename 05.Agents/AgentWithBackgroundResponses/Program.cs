using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Responses;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

#pragma warning disable OPENAI001
ChatClientAgent agent = new OpenAIClient(apiKey)
  .GetResponsesClient()
  .AsAIAgent(model, """
    You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    You have to break down the provided complex commands into the basic moves you know.
    Respond with the moves and their parameters (angle or distance), and provide additional explanations.
    """);

var query = """  
  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then return to the original path."
  """;
ColorHelper.PrintColoredLine("User:", ConsoleColor.Yellow);
ColorHelper.PrintColoredLine(query, ConsoleColor.White);

// Create a session for the agent to maintain conversation context
AgentSession session = await agent.CreateSessionAsync();

// AllowBackgroundResponses and ContinuationToken are for evaluation purposes only and is subject to change or removal in future updates.
AgentRunOptions options = new() { AllowBackgroundResponses = true };
AgentResponse response = await agent.RunAsync(query, session, options);

ResponseStatus? initialStatus = (response.AsChatResponse().RawRepresentation as ResponseResult)?.Status;
ColorHelper.PrintColoredLine($"\n[INITIAL] Assistant:", ConsoleColor.Yellow);
ColorHelper.PrintColoredLine($"Response Status: {initialStatus}", ConsoleColor.Yellow);

#pragma warning disable MEAI001
ColorHelper.PrintColoredLine($"Has Continuation Token: {response.ContinuationToken is not null}", ConsoleColor.Yellow);
ColorHelper.PrintColoredLine(response.Text, ConsoleColor.White);

const int PollingDelayMs = 1000;

while (response.ContinuationToken is not null)
{
  await Task.Delay(PollingDelayMs);
  Console.Write(".");

  options.ContinuationToken = response.ContinuationToken;
  response = await agent.RunAsync([], session, options);
}

ResponseStatus? finalStatus = (response.AsChatResponse().RawRepresentation as ResponseResult)?.Status;
ColorHelper.PrintColoredLine($"\n\n[FINAL] Assistant:", ConsoleColor.Yellow);
ColorHelper.PrintColoredLine($"Response Status: {finalStatus}", ConsoleColor.Yellow);
ColorHelper.PrintColoredLine(response.Text, ConsoleColor.White);
