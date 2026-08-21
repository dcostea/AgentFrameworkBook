using Microsoft.Extensions.AI;  //❶
using System.ComponentModel;

public static class MotorTools  //❷
{
  private const int Delay = 1000;  //❸

  static public IEnumerable<AITool> AsAITools()  //❹
  {
    yield return AIFunctionFactory.Create(TurnRight);
    yield return AIFunctionFactory.Create(TurnLeft);
    yield return AIFunctionFactory.Create(Stop);
    yield return AIFunctionFactory.Create(Forward);
    yield return AIFunctionFactory.Create(Backward);
  }

  [Description("Basic command: Moves the robot car backward.")]
  public static async Task<string> Backward(
    [Description("The distance (in meters) backward.")] int distance)
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: Backward: {distance}m");
    await Task.Delay(Delay);  //❺
    return $"moved backward for {distance} meters.";
  }

  [Description("Basic command: Moves the robot car forward.")]
  public static async Task<string> Forward(
    [Description("The distance (in meters) to move the robot car forward.")]
    int distance)
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: Forward: {distance}m");
    await Task.Delay(Delay);
    return $"moved forward for {distance} meters.";
  }

  [Description("Basic command: Stops the robot car.")]
  public static async Task<string> Stop()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: Stop");
    await Task.Delay(Delay);
    return "stopped.";
  }

  [Description("Basic command: Turns the robot car anticlockwise.")]
  public static async Task<string> TurnLeft(
    [Description("The angle (in degrees) to turn the robot car anticlockwise.")]
    int angle)
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: TurnLeft: {angle}°");
    await Task.Delay(Delay);
    return $"turned anticlockwise {angle}°.";
  }

  [Description("Basic command: Turns the robot car clockwise.")]
  public static async Task<string> TurnRight(
    [Description("The angle (in degrees) to turn the robot car clockwise.")]
    int angle)
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: TurnRight: {angle}°");
    await Task.Delay(Delay);
    return $"turned clockwise {angle}°.";
  }
}
