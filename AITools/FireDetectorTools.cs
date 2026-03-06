using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace AITools;

public static class FireDetectorTools
{
  private const int Delay = 500; // x milliseconds delay for mocking an action

  [/*KernelFunction("capture_camera_feed"), */Description("Use computer vision to visually confirm fire presence and size.")]
  public static async Task<string> CaptureCameraFeedAsync()
  {
    // Simulate capturing camera feed and converting it to text
    var random = new Random();
    var isFire = random.Next(0, 4); // Simulate true or false
    var feed = isFire == 0
        ? "No fire detected in the camera feed."
        : "Fire detected in the camera feed!";
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] FIRE DETECTOR: CAMERA FEED {feed}");
    await Task.Delay(Delay);
    return feed;
  }

  [/*KernelFunction("sound_alarm"), */Description("Trigger an audible or visual alarm to warn nearby humans or systems.")]
  public static async Task SoundAlarmAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] FIRE DETECTOR: Sounding fire alarm!");
    await Task.Delay(Delay);
  }

  [/*KernelFunction("start_water_sprinkle"), */Description("Activate sprinklers to suppress the fire.")]
  public static async Task StartWaterSprinkleAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] FIRE DETECTOR: Starting water sprinkle.");
    await Task.Delay(Delay);
  }

  [/*KernelFunction("stop_water_sprinkle"), */Description("Stop sprinklers when fire is extinguished.")]
  public static async Task StopWaterSprinkleAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] FIRE DETECTOR: Stopping water sprinkle.");
    await Task.Delay(Delay);
  }

  static public IEnumerable<AITool> AsAITools()
  {
    yield return AIFunctionFactory.Create(StartWaterSprinkleAsync);
    yield return AIFunctionFactory.Create(StopWaterSprinkleAsync);
    yield return AIFunctionFactory.Create(SoundAlarmAsync);
    yield return AIFunctionFactory.Create(CaptureCameraFeedAsync);
  }
}
