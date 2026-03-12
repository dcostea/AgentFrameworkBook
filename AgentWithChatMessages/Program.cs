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

List<Microsoft.Extensions.AI.ChatMessage> conversation = [
  new(ChatRole.User, """
    Complex command: 
    "There is a tree directly in front of the car. Avoid it and then return to the original path.
    """),
  new(ChatRole.Assistant, """
    ```json
    [{"move": "stop"}, {"move": "turn", "direction": "right", "angle": 90}, {"move": "forward", "distance": "5" }, {"move": "turn", "direction": "left", "angle": 90}, {"move": "forward", "distance": "5"}, {"move": "turn", "direction": "left", "angle": 90}, {"move": "forward", "distance": "5"}, {"move": "turn", "direction": "right", "angle": 90}]
    ```
    """),
  new(ChatRole.User, """
    What was your second last basic move?
    """),
];

AgentResponse response = await agent.RunAsync(conversation);
Console.WriteLine(response.Text);
