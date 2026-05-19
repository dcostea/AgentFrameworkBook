using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;

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

var modelName = "gemma4:e4b";
//var modelName = "ministral-3";
//var modelName = "mistral-small3.1";
var ollamaServer = "http://localhost:11434";

var ollamaApiClient = new OllamaApiClient(new Uri(ollamaServer), modelName);

//Quantization
////var request = new CreateModelRequest
////{
//// Model = "mistral-small-3.1:q4_k_m",   // name for the new quantized model
//// From = "mistral-small-3.1:fp16",      // must be an FP16/FP32 source
//// Quantize = "q4_K_M",                  // the quantization level
//// Stream = true
////};

////await foreach (var response1 in ollamaApiClient.CreateModelAsync(request))
////{
////  Console.WriteLine(response1?.Status);
////}

List<ChatMessage> messages = [
    new(ChatRole.System, systemMessage),
    new(ChatRole.User, userMessage)
];

ChatResponse response = await ((IChatClient)ollamaApiClient).GetResponseAsync(messages);
messages.AddRange(response.Messages);

Console.WriteLine(response);
