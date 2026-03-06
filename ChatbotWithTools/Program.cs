using AITools;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.ClientModel;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var endpoint = configuration["AzureOpenAI:Endpoint"]!;
var apiKey = configuration["AzureOpenAI:ApiKey"]!;
var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

IChatClient baseClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new ApiKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient();

// Suppress MEAI001 for MessageCountingChatReducer or SummarizingChatReducer usage
#pragma warning disable MEAI001
IChatClient chatClient = new ChatClientBuilder(baseClient)
    .UseFunctionInvocation()
    .UseChatReducer(new MessageCountingChatReducer(10))
    ////.UseChatReducer(new SummarizingChatReducer(baseClient, 20, 10))
    .Build();

var system = """
    You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    You have to break down the provided complex commands into the basic moves you know.
    Do not respond with reasoning, comments, or any additional text.
    """;

List<ChatMessage> conversation = [
    new ChatMessage(ChatRole.System, system)
];
Console.WriteLine($"System:\n{system}\n");

while (true)
{
    Console.Write("User: ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input)) break;
    var query = $"""  
    ## Complex command: 
    "{input}"
    """;

    conversation.Add(new ChatMessage(ChatRole.User, query));

    ChatOptions options = new()
    {
        Temperature = 0.1F,
        ToolMode = ChatToolMode.Auto,
        AllowMultipleToolCalls = true,
        Tools = [.. MotorTools.AsAITools()],
    };

    ChatResponse response = await chatClient.GetResponseAsync(conversation, options);
    Console.WriteLine($"\nAssistant: {response.Text}");
    conversation.AddRange(response.Messages);
}
