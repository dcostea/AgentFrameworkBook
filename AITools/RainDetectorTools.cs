using Microsoft.Extensions.AI;
using Plugins.Enums;
using System.ComponentModel;

namespace AITools;

public static class RainDetectorTools
{
  private const int Delay = 1000; // x milliseconds delay for mocking an action

  [/*KernelFunction("start_wipers"), */Description("Start wipers for the droplet level detected by the sensor (e.g., light drizzle vs heavy rain).")]
  public static async Task<string> StartWipersAsync(DropletLevel dropletLevel)
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] RAIN DETECTOR: Starting wipers for droplet level: {dropletLevel}.");
    await Task.Delay(Delay);
    return $"Wipers have started for droplet level: {dropletLevel}.";
  }

  [/*KernelFunction("stop_wipers"), */Description("Turn off wipers once no droplets are detected for a predefined duration (e.g., 10 seconds).")]
  public static async Task<string> StopWipersAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] RAIN DETECTOR: Stopping wipers.");
    await Task.Delay(Delay);
    return "Wipers have stopped.";
  }

  static public IEnumerable<AITool> AsAITools()
  {
    yield return AIFunctionFactory.Create(StopWipersAsync);
    yield return AIFunctionFactory.Create(StartWipersAsync);
  }
}

