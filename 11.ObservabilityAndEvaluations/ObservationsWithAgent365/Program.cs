using AITools;
using Azure.Core;
using Azure.Identity;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Extensions.AI;
using Microsoft.OpenTelemetry;
using OpenAI;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];
var tenantId = configuration["Agent365:TenantId"];
var clientId = configuration["Agent365:ClientId"];
var clientSecret = configuration["Agent365:ClientSecret"];

var builder = Host.CreateApplicationBuilder(args);

var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var tokenRequestContext = new TokenRequestContext(
  ["api://9b975845-388f-4429-889e-eab1ef63949c/.default"]);

builder.UseMicrosoftOpenTelemetry(options =>
{
  options.Exporters = ExportTarget.Agent365;
  options.Agent365.TokenResolver = async (_, _) =>
  {
    AccessToken accessToken = await credential.GetTokenAsync(tokenRequestContext);
    return accessToken.Token;
  };
  options.Agent365.UseS2SEndpoint = true;
});

var host = builder.Build();
await host.StartAsync();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();

var agent = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient()
  .AsBuilder()
  .UseFunctionInvocation()
  .Build()
  .AsAIAgent(
    name: "RobotCarDemoAgent",
    instructions: """
      You are an AI assistant controlling a robot car.
      The available robot car permitted moves are forward, backward, turn left, turn right, and stop.
      """,
    tools: [.. MotorTools.AsAITools()])
  .AsBuilder()
  .UseLogging(loggerFactory)
  .Build();

var session = await agent.CreateSessionAsync();

while (true)
{
  ColorHelper.PrintColoredLine("You (or 'exit' to quit): ", ConsoleColor.Cyan);
  var userInput = Console.ReadLine();

  if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
    break;

  using var baggageScope = new BaggageBuilder()
    .TenantId(tenantId)
    .AgentId(clientId)
    .Build();

  var response = await agent.RunAsync(userInput, session);

  ColorHelper.PrintColoredLine($"RESPONSE: {response.Text}", ConsoleColor.Green);
}

await host.StopAsync();
