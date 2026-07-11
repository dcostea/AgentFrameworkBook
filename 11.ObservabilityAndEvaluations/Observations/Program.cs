using AITools;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Observations;
using OpenAI;
using OpenTelemetry.Metrics;

using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

const string SourceName = "OpenTelemetry.RobbyAgent";

builder.Services.AddOpenTelemetry()
  .WithTracing(tracing => tracing
    .AddSource(SourceName)
    .AddConsoleExporter()
    .AddOtlpExporter())
  .WithMetrics(metrics => metrics
    .AddMeter(SourceName)
    .AddConsoleExporter()
    .AddOtlpExporter());

var host = builder.Build();
await host.StartAsync();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var meterFactory = host.Services.GetRequiredService<IMeterFactory>();

using var activitySource = new ActivitySource(SourceName);
using var meter = meterFactory.Create(SourceName);
var directionChangeCounter = meter.CreateCounter<int>("robot_direction_change_total", description: "Total times the robot changed direction between forward and backward");
var directionChangeTracker = new DirectionChangeTracker(activitySource, directionChangeCounter);

ColorHelper.PrintColoredLine("""
  === OpenTelemetry Console Demo ===
  This demo shows OpenTelemetry integration with the Agent Framework.
  Telemetry data is exported to the console.
  Type your message and press Enter. Type 'exit' or empty message to quit.
  """, ConsoleColor.Yellow);

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
  .UseOpenTelemetry(SourceName)
  .Use(directionChangeTracker.TrackDirectionChangeAsync)
  .Build();

var session = await agent.CreateSessionAsync();

while (true)
{
  ColorHelper.PrintColoredLine("You (or 'exit' to quit): ", ConsoleColor.Cyan);
  var userInput = Console.ReadLine();

  if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
    break;

  var response = await agent.RunAsync(userInput, session);

  ColorHelper.PrintColoredLine($"RESPONSE: {response.Text}", ConsoleColor.Green);
}

await host.StopAsync();
