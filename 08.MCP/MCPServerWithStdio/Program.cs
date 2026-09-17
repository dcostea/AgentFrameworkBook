using MCPServerWithStdio.Prompts;
using MCPServerWithStdio.Resources;
using MCPServerWithStdio.Tools;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(
    outputTemplate: "[{Timestamp:HH:mm:ss:fff}] {Message:lj}{NewLine}",
    standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
  .CreateLogger();

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Add MCP server with stdio transport, tools, prompts, and resources
builder.Services
  .AddMcpServer(options =>
  {
    options.ServerInfo = new Implementation
    {
      Name = "Motors Server",
      Version = "1.0.0",
    };
    options.InitializationTimeout = TimeSpan.FromSeconds(10);
  })
  .WithStdioServerTransport()
  // Registering tools, prompts and resources in the server
  .WithTools<MotorTools>()
  .WithTools<MaintenanceTools>()
  .WithPrompts<MotorPrompts>()
  .WithResources<MotorResources>()
  // Only calls that opt into Tasks are offloaded; ordinary calls still work.
  .WithTasks(new InMemoryMcpTaskStore { DefaultPollIntervalMs = 250 });

Log.Information("Starting MCP Server running with Stdio transport type");

var app = builder.Build();
app.Run();
