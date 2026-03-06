using MCPServerWithStdio.Prompts;
using MCPServerWithStdio.Resources;
using MCPServerWithStdio.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] {Message:lj}{NewLine}")
  .CreateLogger();

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

McpServerOptions options = new()
{
  ServerInfo = new Implementation
  {
    Name = "Motors Server",
    Version = "1.0.0",
  },
  InitializationTimeout = TimeSpan.FromSeconds(10),
};

builder.Services
  .AddMcpServer(opt =>
  {
    opt.ServerInfo = options.ServerInfo;
    opt.InitializationTimeout = options.InitializationTimeout;
  })
  .WithStdioServerTransport()
  .WithTools<MotorTools>()
  .WithPrompts<MotorPrompts>()
  .WithResources<MotorResources>();

Console.WriteLine("Starting MCP Server with Stdio transport type.");

var app = builder.Build();
app.Run();
