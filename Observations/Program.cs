using AITools;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using System.Diagnostics;
using System.Diagnostics.Metrics;

const string SourceName = "OpenTelemetry.ConsoleApp";

Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Information()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
  .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: false);

builder.AddServiceDefaults();

builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddOpenTelemetry()
  .WithTracing(tracing => tracing
    .AddSource(SourceName)
    .AddSource("*Microsoft.Agents.AI")
    .AddConsoleExporter())
  .WithMetrics(metrics => metrics
    .AddMeter(SourceName)
    .AddMeter("*Microsoft.Agents.AI")
    .AddConsoleExporter());

/*
 using var instrumentedChatClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
        .AsIChatClient() // Converts a native OpenAI SDK ChatClient into a Microsoft.Extensions.AI.IChatClient
        .AsBuilder()
        .UseFunctionInvocation()
        .UseOpenTelemetry(sourceName: SourceName, configure: (cfg) => cfg.EnableSensitiveData = true) // enable telemetry at the chat client level
        .Build();

appLogger.LogInformation("Creating Agent with OpenTelemetry instrumentation");
// Create the agent with the instrumented chat client
var agent = new ChatClientAgent(instrumentedChatClient,
    name: "OpenTelemetryDemoAgent",
    instructions: "You are a helpful assistant that provides concise and informative responses.",
    tools: [AIFunctionFactory.Create(GetWeatherAsync)])
    .AsBuilder()
    .UseOpenTelemetry(sourceName: SourceName, configure: (cfg) => cfg.EnableSensitiveData = true) // enable telemetry at the agent level
    .Build();
 
 */

var host = builder.Build();
await host.StartAsync();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("Observations");
var meterFactory = host.Services.GetRequiredService<IMeterFactory>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

using var activitySource = new ActivitySource(SourceName);
using var meter = meterFactory.Create(SourceName);
var consecutiveCommandCounter = meter.CreateCounter<int>("robot_consecutive_same_command_total", description: "Total times the same robot command was repeated consecutively");

ColorHelper.PrintColoredLine("""
  === OpenTelemetry Console Demo ===
  This demo shows OpenTelemetry integration with the Agent Framework.
  Telemetry data is exported to the Aspire Dashboard.
  Type your message and press Enter. Type 'exit' or empty message to quit.
  """, ConsoleColor.Yellow);

logger.LogInformation("OpenTelemetry Console Demo application started");

var agent = new OpenAIClient(configuration["OpenAI:ApiKey"])
  .GetChatClient(configuration["OpenAI:ModelId"])
  .AsIChatClient()
  .AsBuilder()
  .UseFunctionInvocation()
  .UseOpenTelemetry(sourceName: SourceName)
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
  .Build();

var session = await agent.CreateSessionAsync();

using var sessionActivity = activitySource.StartActivity("Agent Session");
sessionActivity?.SetTag("agent.name", "RobotCarDemoAgent");

logger.LogInformation("Robot Car Agent session started with ID: {AgentId}", agent.Id);

string? previousCommand = null;

while (true)
{
  ColorHelper.PrintColoredLine("You (or 'exit' to quit): ", ConsoleColor.Cyan);
  var userInput = Console.ReadLine();

  if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
    break;

  logger.LogInformation("User input: {UserInput}", userInput);

  using var activity = activitySource.StartActivity("Robot Car Agent Interaction");
  activity?.SetTag("user.input", userInput);

  var response = await agent.RunAsync(userInput, session);

  ColorHelper.PrintColoredLine($"RESPONSE: {response.Text}", ConsoleColor.Green);
  logger.LogInformation("Robot Car Agent response: {Response}", response.Text);

  string? currentCommand = (response.Text ?? "") switch
  {
    var t when t.Contains("forward",  StringComparison.OrdinalIgnoreCase) => "forward",
    var t when t.Contains("backward", StringComparison.OrdinalIgnoreCase) => "backward",
    var t when t.Contains("left",     StringComparison.OrdinalIgnoreCase) => "turn_left",
    var t when t.Contains("right",    StringComparison.OrdinalIgnoreCase) => "turn_right",
    var t when t.Contains("stop",     StringComparison.OrdinalIgnoreCase) => "stop",
    _ => null
  };
  if (currentCommand is not null && currentCommand == previousCommand)
    consecutiveCommandCounter.Add(1, new KeyValuePair<string, object?>("command", currentCommand));
  previousCommand = currentCommand;
}

logger.LogInformation("Robot Car Agent session completed.");

await host.StopAsync();
