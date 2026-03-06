using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;
using System.ComponentModel;

#pragma warning disable MCPEXP001 // Tasks are experimental in MCP SDK v1.0

namespace MCPServerWithStdio.Tools;

[McpServerToolType, Description("Robot car motors plugin.")]
public class MotorTools
{
  private const int Delay = 100; // x seconds delay for mocking an action

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

  /// <summary>
  /// Runs a full diagnostics check on all motors. Always runs as a task due to the long execution time.
  /// </summary>
  [McpServerTool(Name = "run_diagnostics", TaskSupport = ToolTaskSupport.Required)]
  [Description("Runs a full diagnostics check on all robot car motors. This is a long-running operation that always runs as a task.")]
  public static async Task<string> RunDiagnosticsAsync(
    [Description("Whether to include a detailed report with individual motor metrics.")] bool detailed,
    CancellationToken cancellationToken)
  {
    Log.Information("MOTORS: Diagnostics started (detailed: {Detailed})", detailed);

    string[] motors = ["front-left", "front-right", "rear-left", "rear-right"];

    foreach (string motor in motors)
    {
      cancellationToken.ThrowIfCancellationRequested();
      Log.Information("MOTORS: Checking motor {Motor}...", motor);
      await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }

    Log.Information("MOTORS: Diagnostics completed");

    return detailed
      ? $"Diagnostics complete. All {motors.Length} motors passed. RPM: 1200, Temp: 42°C, Voltage: 12.1V per motor."
      : $"Diagnostics complete. All {motors.Length} motors passed.";
  }
}
