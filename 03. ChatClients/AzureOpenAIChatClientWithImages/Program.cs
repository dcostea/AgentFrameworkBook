using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.ClientModel;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var endpoint = configuration["AzureOpenAI:AudioEndpoint"]!;
var apiKey = configuration["AzureOpenAI:AudioApiKey"]!;
var deploymentName = configuration["AzureOpenAI:AudioDeploymentName"]!;

IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
  .GetChatClient(deploymentName)
  .AsIChatClient();

var system = """
  ## Persona
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    
  ## Action
  You have to break down the provided complex commands into the basic moves you know.
    
  ## Template
  Respond with a JSON array like [move1, move2, move3].
  Do not respond with reasoning, comments, or any additional text.
  """;

byte[] imageBytes = File.ReadAllBytes(@"Data/path.jpg");
byte[] audioBytes = File.ReadAllBytes(@"Data/task.mp3");

List<ChatMessage> conversation = [
  new ChatMessage(ChatRole.System, system),
  ////new(ChatRole.User, [
  ////  new TextContent("Look at the image and proceed safely."),
  ////  new UriContent(@"http://apexcode.ro/path.jpg", "image/jpeg")
  ////]),
  ////new(ChatRole.User, [
  ////  new TextContent("Look at the image and proceed safely."),
  ////  new DataContent(imageBytes, "image/jpeg")
  ////]),

  ////new(ChatRole.User, [
  ////  new UriContent(new Uri(@"https://apexcode.ro/task.mp3"), "audio/mpeg")
  ////]),
  new(ChatRole.User, [
    new DataContent(audioBytes, "audio/mpeg")
  ]),
];

/*
PNG (.png) → image/png
JPEG (.jpeg, .jpg) → image/jpeg
WEBP (.webp) → image/webp
Non-animated GIF (.gif) → image/gif 

MP3 (.mp3) → audio/mpeg
MP4 (.mp4) → video/mp4 (audio track)
MPEG (.mpeg) → audio/mpeg
MPGA (.mpga) → audio/mpeg
M4A (.m4a) → audio/m4a
WAV (.wav) → audio/wav
WEBM (.webm) → audio/webm
 */


ChatResponse response = await chatClient.GetResponseAsync(conversation);
Console.WriteLine($"\nAssistant (Azure ChatClient): {response.Text}");
