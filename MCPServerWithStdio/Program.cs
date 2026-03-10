using MCPServerWithStdio.Prompts;
using MCPServerWithStdio.Resources;
using MCPServerWithStdio.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] {Message:lj}{NewLine}")
  .CreateLogger();

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

#pragma warning disable MCPEXP001 // Tasks are experimental in MCP SDK v1.0
InMemoryMcpTaskStore taskStore = new();

McpServerOptions options = new()
{
  ServerInfo = new Implementation
  {
    Name = "Motors Server",
    Version = "1.0.0",
  },
  InitializationTimeout = TimeSpan.FromSeconds(10),
  TaskStore = taskStore,
};

builder.Services
  .AddMcpServer(opt =>
  {
    opt.ServerInfo = options.ServerInfo;
    opt.InitializationTimeout = options.InitializationTimeout;
    opt.TaskStore = options.TaskStore;
  })
  .WithStdioServerTransport()
  .WithTools<MotorTools>()
  .WithPrompts<MotorPrompts>()
  .WithResources<MotorResources>();

Log.Information("Starting MCP Server is running with Stdio transport type");

var app = builder.Build();
app.Run();
