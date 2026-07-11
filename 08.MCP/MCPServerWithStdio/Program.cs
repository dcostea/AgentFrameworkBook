using MCPServerWithStdio.Prompts;
using MCPServerWithStdio.Resources;
using MCPServerWithStdio.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] {Message:lj}{NewLine}")
  .CreateLogger();

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

#pragma warning disable MCPEXP001 // Tasks are experimental in MCP SDK v1.0
InMemoryMcpTaskStore taskStore = new();

builder.Services
  .AddMcpServer(options =>
  {
    options.ServerInfo = new Implementation
    {
      Name = "Motors Server",
      Version = "1.0.0",
    };
    options.InitializationTimeout = TimeSpan.FromSeconds(10);
    options.TaskStore = taskStore;
  })
  .WithStdioServerTransport()
  // Registering tools, prompts and resources in the server
  .WithTools<MotorTools>()
  .WithPrompts<MotorPrompts>()
  .WithResources<MotorResources>();

Log.Information("Starting MCP Server running with Stdio transport type");

var app = builder.Build();
app.Run();
