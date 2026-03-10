using MCPServerWithHttp.Prompts;
using MCPServerWithHttp.Resources;
using MCPServerWithHttp.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] {Message:lj}{NewLine}")
  .CreateLogger();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

#pragma warning disable MCPEXP001
InMemoryMcpTaskStore taskStore = new();

McpServerOptions options = new()
{
  ServerInfo = new Implementation
  {
    Name = "MotorsServer",
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
  .WithHttpTransport(o => o.Stateless = true)
  .WithTools<MotorTools>()
  .WithPrompts<MotorPrompts>()
  .WithResources<MotorResources>();

var app = builder.Build();
app.MapMcp();
app.Run();
