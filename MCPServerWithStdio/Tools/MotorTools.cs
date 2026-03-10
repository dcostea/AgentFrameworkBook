using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;
using System.ComponentModel;

namespace MCPServerWithStdio.Tools;

[McpServerToolType, Description("Robot car motors plugin.")]
public class MotorTools
{
  private const int Delay = 100; // x seconds delay for mocking an action

  #pragma warning disable MCPEXP001 // Tasks are experimental in MCP SDK v1.0

  [McpMeta("category", "motor")]
  [McpServerTool(Name = "backward", TaskSupport = ToolTaskSupport.Optional), Description("Basic command: Moves the robot car backward.")]
  public async Task<string> BackwardAsync([Description("The distance (in meters) to move the robot car backward.")] int distance)
  {
    Log.Information("MOTORS: Backward: {Distance}m", distance);
    await Task.Delay(Delay);
    return $"moved backward for {distance} meters.";
  }

  [McpServerTool(Name = "forward", TaskSupport = ToolTaskSupport.Optional), Description("Basic command: Moves the robot car forward.")]
  public async Task<string> ForwardAsync([Description("The distance (in meters) to move the robot car forward.")] int distance)
  {
    Log.Information("MOTORS: Forward: {Distance}m", distance);
    await Task.Delay(Delay);
    return $"moved forward for {distance} meters.";
  }

  [McpServerTool(Name = "stop", TaskSupport = ToolTaskSupport.Optional), Description("Basic command: Stops the robot car.")]
  public async Task<string> StopAsync()
  {
    Log.Information("MOTORS: Stop");
    await Task.Delay(Delay);
    return "stopped.";
  }

  [McpServerTool(Name = "turn_left", TaskSupport = ToolTaskSupport.Optional), Description("Basic command: Turns the robot car anticlockwise.")]
  public async Task<string> TurnLeftAsync([Description("The angle (in ° / degrees) to turn the robot car anticlockwise.")] int angle)
  {
    Log.Information("MOTORS: TurnLeft: {Angle}°", angle);
    await Task.Delay(Delay);
    return $"turned anticlockwise {angle}°.";
  }

  [McpServerTool(Name = "turn_right", TaskSupport = ToolTaskSupport.Optional), Description("Basic command: Turns the robot car clockwise.")]
  public async Task<string> TurnRightAsync([Description("The angle (in ° / degrees) to turn the robot car clockwise.")] int angle)
  {
    Log.Information("MOTORS: TurnRight: {Angle}°", angle);
    await Task.Delay(Delay);
    return $"turned clockwise {angle}°.";
  }

  [McpServerTool(Name = "run_diagnostics", TaskSupport = ToolTaskSupport.Required)]
  [Description("Runs a full diagnostics check on all robot car motors. Always runs as a background task.")]
  public static async Task<string> RunDiagnosticsAsync()
  {
    await Task.Delay(TimeSpan.FromSeconds(2));

    return "Diagnostics complete. All 4 motors passed.";
  }

  [McpServerTool(Name = "run_diagnostics_with_progress", TaskSupport = ToolTaskSupport.Required)]
  [Description("Runs a full diagnostics check with progress on all robot car motors. Always runs as a background task.")]
  public static async Task<string> RunDiagnosticsWithProgressAsync(
    IProgress<ProgressNotificationValue> progress)
  {
    for (int i = 1; i <= 4; i++)
    {
      await Task.Delay(TimeSpan.FromSeconds(2));
      progress.Report(new() { Progress = i, Total = 4 });
    }

    return "Diagnostics complete. All 4 motors passed.";
  }
}
