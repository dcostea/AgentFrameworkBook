using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using System.ClientModel;
using Microsoft.Extensions.AI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var endpoint = configuration["AzureOpenAI:Endpoint"]!;
var apiKey = configuration["AzureOpenAI:ApiKey"]!;
var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

var query = """
    You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
    You have to break down the provided complex commands into the basic moves you know.
    Respond only with the moves and their parameters (angle or distance), and provide additional explanations.

    Complex command: 
    "There is a tree directly in front of the car. Avoid it and then return to the original path."
    """;
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("User:");
Console.ResetColor();
Console.WriteLine(query);


// GetOpenAIResponseClient is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable OPENAI001
IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
    .GetResponsesClient(deploymentName)
    .AsIChatClient();

try
{
    // AllowBackgroundResponses and ContinuationToken are for evaluation purposes only and is subject to change or removal in future updates.
    #pragma warning disable MEAI001 // Suppress this diagnostic to proceed.
    ChatOptions options = new() { AllowBackgroundResponses = true };
    ChatResponse chatResponse = await chatClient.GetResponseAsync(query, options);
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"\n[INITIAL] Assistant:");
    Console.WriteLine($"Finish Reason: {chatResponse.FinishReason?.Value ?? "(null)"}");
    Console.WriteLine($"Has Continuation Token: {chatResponse.ContinuationToken is not null}");
    Console.ResetColor();
    Console.WriteLine(chatResponse.Text);

    var token = chatResponse.ContinuationToken;
    if (token is not null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("POLLING");
        Console.ResetColor();

        const int maxPollingAttempts = 60;
        const int pollingDelayMs = 1000;
        int attempts = 0;

        while (token is not null && attempts < maxPollingAttempts)
        {
            await Task.Delay(pollingDelayMs);
            Console.Write(".");
            attempts++;

            ChatOptions resumeOptions = new() { ContinuationToken = token };
            chatResponse = await chatClient.GetResponseAsync([], resumeOptions);
            token = chatResponse.ContinuationToken;
        }

        if (token is not null)
        {
            Console.WriteLine($"\n\nPolling timeout after {maxPollingAttempts} attempts.");
            Console.WriteLine("The background response did not complete in time.");
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n\n[FINAL] Assistant:");
        Console.WriteLine($"Finish Reason: {chatResponse.FinishReason?.Value ?? "(null)"}");
        Console.ResetColor();
        Console.WriteLine(chatResponse.Text);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(string.IsNullOrEmpty(chatResponse.Text)
            ? "\nNo continuation token - response completed immediately (no text)."
            : $"\nNo continuation token\n[COMPLETED]Assistant: {chatResponse.Text}");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n\nError: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.ResetColor();
}
