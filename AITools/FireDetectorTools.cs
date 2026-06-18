using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace AITools;

public static class FireDetectorTools
{
  private const int Delay = 500; // x milliseconds delay for mocking an action

  [Description("Trigger an audible or visual alarm to warn nearby humans or systems.")]
  public static async Task<string> SoundAlarmAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] FIRE DETECTOR: Sounding fire alarm!");
    await Task.Delay(Delay);
    return "Fire alarm sounded.";
  }

  [Description("Activate sprinklers to suppress the fire.")]
  public static async Task<string> StartWaterSprinkleAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] FIRE DETECTOR: Starting water sprinkle.");
    await Task.Delay(Delay);
    return "Water sprinkler started — fire suppression in progress.";
  }

  [Description("Stop sprinklers when fire is extinguished.")]
  public static async Task<string> StopWaterSprinkleAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] FIRE DETECTOR: Stopping water sprinkle.");
    await Task.Delay(Delay);
    return "Water sprinkler stopped.";
  }

  static public IEnumerable<AITool> AsAITools()
  {
    yield return AIFunctionFactory.Create(StartWaterSprinkleAsync);
    yield return AIFunctionFactory.Create(StopWaterSprinkleAsync);
    yield return AIFunctionFactory.Create(SoundAlarmAsync);
  }
}

