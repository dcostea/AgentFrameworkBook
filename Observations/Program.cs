using AITools;
using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Diagnostics;
using System.Diagnostics.Metrics;

Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Information()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
  .CreateLogger();

ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(Log.Logger));
var sessionId = Guid.NewGuid().ToString("N");
var logger = loggerFactory.CreateLogger<Program>();

const string SourceName = "OpenTelemetry.ConsoleApp";
const string ServiceName = "AgentOpenTelemetry";

var resourceBuilder = ResourceBuilder.CreateDefault()
  .AddService(ServiceName, serviceVersion: "1.0.0")
  .AddTelemetrySdk();

// Setup tracing with console exporter
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
  .SetResourceBuilder(resourceBuilder)
  .AddSource(SourceName)
  .AddSource("*Microsoft.Agents.AI")
  .AddConsoleExporter()
  .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
  .SetResourceBuilder(resourceBuilder)
  .AddMeter(SourceName)
  .AddMeter("*Microsoft.Agents.AI")
  //.AddConsoleExporter() // uncomment to see full metric dumps (histograms, counters) in the console
  .Build();

using var activitySource = new ActivitySource(SourceName);
using var meter = new Meter(SourceName);

// Create custom metrics
var interactionCounter = meter.CreateCounter<int>("agent_interactions_total", description: "Total number of agent interactions");
var responseTimeHistogram = meter.CreateHistogram<double>("agent_response_time_seconds", description: "Agent response time in seconds");

ColorHelper.PrintColoredLine("""
  === OpenTelemetry Console Demo ===
  This demo shows OpenTelemetry integration with the Agent Framework.
  Telemetry data is exported to the console.
  Type your message and press Enter. Type 'exit' or empty message to quit.
  """, ConsoleColor.Yellow);

logger.LogInformation("OpenTelemetry Console Demo application started");

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var model = configuration["OpenAI:ModelId"];
var apiKey = configuration["OpenAI:ApiKey"];

using var chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient()
  .AsBuilder()
  .UseFunctionInvocation()
  .UseLogging(loggerFactory)
  .UseOpenTelemetry(sourceName: SourceName)
  .Build();

logger.LogInformation("Creating Agent with OpenTelemetry instrumentation");

var agent = chatClient.AsAIAgent(
  name: "OpenTelemetryDemoAgent",
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

logger.LogInformation("Agent created successfully with ID: {AgentId}", agent.Id);

// Create a parent span for the entire agent session
using var sessionActivity = activitySource.StartActivity("Agent Session");
ColorHelper.PrintColoredLine($"Trace ID: {sessionActivity?.TraceId}", ConsoleColor.Yellow);

sessionActivity?
  .SetTag("agent.name", "OpenTelemetryDemoAgent")
  .SetTag("session.id", sessionId)
  .SetTag("session.start_time", DateTimeOffset.UtcNow.ToString("O"));

logger.LogInformation("Starting agent session with ID: {SessionId}", sessionId);

var interactionCount = 0;

while (true)
{
  ColorHelper.PrintColoredLine("You (or 'exit' to quit): ", ConsoleColor.Cyan);
  var userInput = Console.ReadLine();

  if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
  {
    logger.LogInformation("User requested to exit the session");
    break;
  }

  interactionCount++;

  // Create a child span for each individual interaction
  using var activity = activitySource.StartActivity("Agent Interaction");
  activity?
    .SetTag("user.input", userInput)
    .SetTag("agent.name", "OpenTelemetryDemoAgent")
    .SetTag("interaction.number", interactionCount);

  var stopwatch = Stopwatch.StartNew();

  // Run the agent (this will create its own internal telemetry spans)
  var response = await agent.RunAsync(userInput, session);

  ColorHelper.PrintColoredLine($"RESPONSE: {response.Text}", ConsoleColor.Green);

  stopwatch.Stop();
  var responseTime = stopwatch.Elapsed.TotalSeconds;

  // Record metrics
  interactionCounter.Add(1, new KeyValuePair<string, object?>("status", "success"));
  responseTimeHistogram.Record(responseTime,
      new KeyValuePair<string, object?>("status", "success"));
}

// Add session summary to the parent span
sessionActivity?
  .SetTag("session.total_interactions", interactionCount)
  .SetTag("session.end_time", DateTimeOffset.UtcNow.ToString("O"));

logger.LogInformation("Agent session completed. Total interactions: {TotalInteractions}", interactionCount);
logger.LogInformation("OpenTelemetry Console Demo application shutting down");
