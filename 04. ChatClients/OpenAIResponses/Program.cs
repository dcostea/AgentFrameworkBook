using Microsoft.Extensions.Configuration;
using OpenAI;
using Microsoft.Extensions.AI;
using System.ClientModel;
using OpenAI.Responses;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

var query = """
  ## Persona
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    
  ## Action
  You have to break down the provided complex commands into the basic moves you know.
    
  ## Complex Command
  There is a tree in front of the car. Avoid it and resume the original path.

  ## Template
  Respond with a JSON array like [move1, move2, move3].
  Do not respond with reasoning, comments, or any additional text.
  """;
Console.WriteLine($"User: {query}");

#pragma warning disable OPENAI001

// Using OpenAIClient directly with Responses API
ResponsesClient responsesClient = new OpenAIClient(apiKey)
  .GetResponsesClient();
ClientResult<ResponseResult> response = responsesClient.CreateResponse(model, query);
Console.WriteLine($"\nAssistant (OpenAI Responses): {response.Value.GetOutputText()}");

// Using IChatClient interface
IChatClient chatClient = new OpenAIClient(apiKey)
  .GetResponsesClient()
  .AsIChatClient(model);
ChatResponse chatResponse = await chatClient.GetResponseAsync(query);
Console.WriteLine($"\nAssistant (IChatClient): {chatResponse.Text}");
