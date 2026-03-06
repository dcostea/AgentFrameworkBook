using MCPServerWithHttp.Prompts;
using MCPServerWithHttp.Resources;
using MCPServerWithHttp.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff}] {Message:lj}{NewLine}")
  .CreateLogger();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

McpServerOptions options = new()
{
  ServerInfo = new Implementation
  {
    Name = "MotorsServer",
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
  .WithHttpTransport(o => o.Stateless = true)
  .WithTools<MotorTools>()
  .WithPrompts<MotorPrompts>()
  .WithResources<MotorResources>();

Console.WriteLine($"Starting MCP Server with HTTP transport type at: {builder.Configuration["urls"]}.");

var app = builder.Build();
app.MapMcp();
app.Run();
