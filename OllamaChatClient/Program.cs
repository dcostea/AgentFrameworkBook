using Microsoft.Extensions.AI;
using OllamaSharp;

var systemMessage = """
    ### Persona  
    You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.  

    ### Action  
    You have to break down the provided complex commands into basic moves you know.  

    ### Template  
    Respond only with the permitted moves, without any additional explanations.  
    """;

var userMessage = """  
    Complex command: 
    "There is a tree directly in front of the car. Avoid it and then come back to the original path."  
    """;

//var modelName = "gemma3:4b";
var modelName = "ministral-3";
//var modelName = "mistral-small3.1";
var ollamaServer = "http://localhost:11434";

IChatClient ollamaApiClient = new OllamaApiClient(new Uri(ollamaServer), modelName);

List<ChatMessage> messages = [
    new(ChatRole.System, systemMessage),
    new(ChatRole.User, userMessage)
];

ChatResponse response = await ollamaApiClient.GetResponseAsync(messages);
messages.AddRange(response.Messages);

Console.WriteLine(response);
