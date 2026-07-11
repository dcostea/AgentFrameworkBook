using MCPServerWithHttp.Prompts;
using MCPServerWithHttp.Resources;
using MCPServerWithHttp.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] {Message:lj}{NewLine}")
  .CreateLogger();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

#pragma warning disable MCPEXP001
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
  .WithHttpTransport(o => o.Stateless = true)
  .WithTools<MotorTools>()
  .WithPrompts<MotorPrompts>()
  .WithResources<MotorResources>();

var app = builder.Build();
app.MapMcp();
app.Run();
