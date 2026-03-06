using ModelContextProtocol.Server;
using Serilog;
using System.ComponentModel;

namespace MCPServerWithHttp.Tools;

[McpServerToolType, Description("Robot car motors plugin.")]
public class MotorTools
{
  private const int Delay = 100; // x seconds delay for mocking an action

  [McpMeta("category", "motor")]
  [McpServerTool(Name = "backward"), Description("Basic command: Moves the robot car backward.")]
  public async Task<string> BackwardAsync([Description("The distance (in meters) to move the robot car backward.")] int distance)
  {
    Log.Information("MOTORS: Backward: {Distance}m", distance);
    await Task.Delay(Delay);
    return $"moved backward for {distance} meters.";
  }

  [McpServerTool(Name = "forward"), Description("Basic command: Moves the robot car forward.")]
  public async Task<string> ForwardAsync([Description("The distance (in meters) to move the robot car forward.")] int distance)
  {
    Log.Information("MOTORS: Forward: {Distance}m", distance);
    await Task.Delay(Delay);
    return $"moved forward for {distance} meters.";
  }

  [McpServerTool(Name = "stop"), Description("Basic command: Stops the robot car.")]
  public async Task<string> StopAsync()
  {
    Log.Information("MOTORS: Stop");
    await Task.Delay(Delay);
    return "stopped.";
  }

  [McpServerTool(Name = "turn_left"), Description("Basic command: Turns the robot car anticlockwise.")]
  public async Task<string> TurnLeftAsync([Description("The angle (in ° / degrees) to turn the robot car anticlockwise.")] int angle)
  {
    Log.Information("MOTORS: TurnLeft: {Angle}°", angle);
    await Task.Delay(Delay);
    return $"turned anticlockwise {angle}°.";
  }

  [McpServerTool(Name = "turn_right"), Description("Basic command: Turns the robot car clockwise.")]
  public async Task<string> TurnRightAsync([Description("The angle (in ° / degrees) to turn the robot car clockwise.")] int angle)
  {
    Log.Information("MOTORS: TurnRight: {Angle}°", angle);
    await Task.Delay(Delay);
    return $"turned clockwise {angle}°.";
  }
}
