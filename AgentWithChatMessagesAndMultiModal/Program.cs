using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

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

byte[] audioBytes = File.ReadAllBytes(@"Data\Task.mp3");
byte[] imageBytes = File.ReadAllBytes(@"Data\Map.png");

List<Microsoft.Extensions.AI.ChatMessage> conversation = [
  ////new ChatMessage(ChatRole.System, system),

  ////new(ChatRole.User, [
  ////  new TextContent("Look at the image of the map and proceed safely."),
  ////  new UriContent(@"http://apexcode.ro/path.jpg", "image/jpeg")
  ////]),
  ////new(ChatRole.User, [
  ////  new TextContent("Look at the image of the map and proceed safely."),
  ////  new UriContent(@"http://apexcode.ro/road_and_tree.png", "image/jpeg")
  ////]),
  new(ChatRole.User, [
    new TextContent("Look at the image of the map and proceed safely."),
    new DataContent(imageBytes, "image/jpeg")
  ]),

  ////new(ChatRole.User, [
  ////  new UriContent(new Uri(@"https://apexcode.ro/task.mp3"), "audio/mpeg")
  ////]),
  ////new(ChatRole.User, [
  ////  new DataContent(audioBytes, "audio/mpeg")
  ////]),
];

AgentResponse response = await agent.RunAsync(conversation);
Console.WriteLine(response.Text);
