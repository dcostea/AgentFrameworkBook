using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

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
Console.WriteLine($"USER: {query}");

// Using OpenAIClient directly
ChatClient openAIChatClient = new OpenAIClient(apiKey)
    .GetChatClient(model);
ClientResult<ChatCompletion> response = openAIChatClient.CompleteChat(query);
Console.WriteLine($"\nAssistant (OpenAI ChatClient): {response.Value.Content.First().Text}");

// Using IChatClient interface
IChatClient chatClient = new OpenAIClient(apiKey)
    .GetChatClient(model)
    .AsIChatClient();
ChatResponse chatResponse = await chatClient.GetResponseAsync(query);
Console.WriteLine($"\nAssistant (IChatClient): {chatResponse.Text}");