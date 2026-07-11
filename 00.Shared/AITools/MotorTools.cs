using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace AITools;

public static class MotorTools
{
  private const int Delay = 1000; // x milliseconds delay for mocking an action

  [Description("Basic command: Moves the robot car backward.")]
  public static async Task<string> BackwardAsync([Description("The distance (in meters) to move the robot car backward.")] int distance)
  {
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: Backward: {distance}m");
    Console.ResetColor();
    await Task.Delay(Delay);
    return $"moved backward for {distance} meters.";
  }

  [Description("Basic command: Moves the robot car forward.")]
  public static async Task<string> ForwardAsync([Description("The distance (in meters) to move the robot car forward.")] int distance)
  {
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: Forward: {distance}m");
    Console.ResetColor();
    await Task.Delay(Delay);
    return $"moved forward for {distance} meters.";
  }

  [Description("Basic command: Stops the robot car.")]
  public static async Task<string> StopAsync()
  {
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: Stop");
    Console.ResetColor();
    await Task.Delay(Delay);
    return "stopped.";
  }

  [Description("Basic command: Turns the robot car anticlockwise.")]
  public static async Task<string> TurnLeftAsync([Description("The angle (in ° / degrees) to turn the robot car anticlockwise.")] int angle)
  {
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: TurnLeft: {angle}°");
    Console.ResetColor();
    await Task.Delay(Delay);
    return $"turned anticlockwise {angle}°.";
  }

  [Description("Basic command: Turns the robot car clockwise.")]
  public static async Task<string> TurnRightAsync([Description("The angle (in ° / degrees) to turn the robot car clockwise.")] int angle)
  {
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MOTORS: TurnRight: {angle}°");
    await Task.Delay(Delay);
    Console.ResetColor();
    return $"turned clockwise {angle}°.";
  }

  static public IEnumerable<AIFunction> AsAITools()
  {
    yield return AIFunctionFactory.Create(TurnRightAsync);
    yield return AIFunctionFactory.Create(TurnLeftAsync);
    yield return AIFunctionFactory.Create(StopAsync);
    yield return AIFunctionFactory.Create(ForwardAsync);
    yield return AIFunctionFactory.Create(BackwardAsync);
  }
}
