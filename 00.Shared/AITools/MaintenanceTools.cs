using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace AITools;

public static class MaintenanceTools
{
  private const int Delay = 500; // milliseconds delay for mocking an action

  [Description("Calibrates all sensors on the robot car.")]
  public static async Task<string> CalibrateSensorsAsync()
  {
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CALIBRATING sensors...");
    Console.ResetColor();
    await Task.Delay(Delay);
    return "All sensors have been calibrated.";
  }

  [Description("Checks the motors of the robot car.")]
  public static async Task<string> CheckMotorsAsync()
  {
    var random = new Random();
    var motorStatus = random.Next(0, 100);
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CHECKING motors. Status: {motorStatus}%");
    Console.ResetColor();
    await Task.Delay(Delay);
    return $"Motors checked. Status: {motorStatus}% efficiency.";
  }

  [Description("Checks the tire pressure of the robot car.")]
  public static async Task<string> CheckTirePressureAsync()
  {
    var random = new Random();
    var pressure = random.Next(30, 35);
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CHECKING tire pressure: {pressure} PSI");
    Console.ResetColor();
    await Task.Delay(Delay);
    return $"Tire pressure is {pressure} PSI.";
  }

  [Description("Cleans the solar panels of the robot car.")]
  public static async Task<string> CleanSolarPanelsAsync()
  {
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CLEANING solar panels...");
    Console.ResetColor();
    await Task.Delay(Delay * 3);
    return "Solar panels have been cleaned.";
  }

  [Description("Checks the battery health of the robot car.")]
  public static async Task<string> CheckBatteryHealthAsync()
  {
    var random = new Random();
    var batteryHealth = random.Next(0, 100);
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CHECKING battery health: {batteryHealth}%");
    Console.ResetColor();
    await Task.Delay(Delay);
    return $"Battery health is at {batteryHealth}%.";
  }

  [Description("Updates the firmware of the robot car.")]
  public static async Task<string> UpdateFirmwareAsync()
  {
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: UPDATING firmware...");
    Console.ResetColor();
    await Task.Delay(Delay * 5);
    return "Firmware has been updated to the latest version.";
  }

  static public IEnumerable<AITool> AsAITools()
  {
    yield return AIFunctionFactory.Create(UpdateFirmwareAsync);
    yield return AIFunctionFactory.Create(CheckBatteryHealthAsync);
    yield return AIFunctionFactory.Create(CleanSolarPanelsAsync);
    yield return AIFunctionFactory.Create(CheckTirePressureAsync);
    yield return AIFunctionFactory.Create(CheckMotorsAsync);
    yield return AIFunctionFactory.Create(CalibrateSensorsAsync);
  }
}
