using MCPServerWithStdio.Prompts;
using MCPServerWithStdio.Resources;
using MCPServerWithStdio.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

#pragma warning disable MCPEXP001 // Tasks are experimental in MCP SDK v1.0

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] {Message:lj}{NewLine}")
  .CreateLogger();

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

var taskStore = new InMemoryMcpTaskStore(
  defaultTtl: TimeSpan.FromMinutes(30),
  pollInterval: TimeSpan.FromSeconds(2),
  maxTasks: 100);

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

Log.Information("Starting MCP Server with Stdio transport type");
Log.Information("Task store: {TaskStore} (TTL: {Ttl}, poll: {Poll}, max: {Max})",
  taskStore.GetType().Name,
  TimeSpan.FromMinutes(30),
  TimeSpan.FromSeconds(2),
  100);

var app = builder.Build();
app.Run();
