using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace AITools;

public static class MaintenanceTools
{
  private const int Delay = 500; // milliseconds delay for mocking an action

  [/*KernelFunction("calibrate_sensors"), */Description("Calibrates all sensors on the robot car.")]
  public static async Task<string> CalibrateSensorsAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CALIBRATING sensors...");
    await Task.Delay(Delay);
    return "All sensors have been calibrated.";
  }

  [/*KernelFunction("check_motors"), */Description("Checks the motors of the robot car.")]
  public static async Task<string> CheckMotorsAsync()
  {
    var random = new Random();
    var motorStatus = random.Next(0, 100);
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CHECKING motors. Status: {motorStatus}%");
    await Task.Delay(Delay);
    return $"Motors checked. Status: {motorStatus}% efficiency.";
  }

  [/*KernelFunction("check_tire_pressure"), */Description("Checks the tire pressure of the robot car.")]
  public static async Task<string> CheckTirePressureAsync()
  {
    var random = new Random();
    var pressure = random.Next(30, 35);
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CHECKING tire pressure: {pressure} PSI");
    await Task.Delay(Delay);
    return $"Tire pressure is {pressure} PSI.";
  }

  [/*KernelFunction("clean_solar_panels"), */Description("Cleans the solar panels of the robot car.")]
  public static async Task<string> CleanSolarPanelsAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CLEANING solar panels...");
    await Task.Delay(Delay * 3);
    return "Solar panels have been cleaned.";
  }

  [/*KernelFunction("check_battery_health"), */Description("Checks the battery health of the robot car.")]
  public static async Task<string> CheckBatteryHealthAsync()
  {
    var random = new Random();
    var batteryHealth = random.Next(0, 100);
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: CHECKING battery health: {batteryHealth}%");
    await Task.Delay(Delay);
    return $"Battery health is at {batteryHealth}%.";
  }

  [/*KernelFunction("update_firmware"), */Description("Updates the firmware of the robot car.")]
  public static async Task<string> UpdateFirmwareAsync()
  {
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] MAINTENANCE: UPDATING firmware...");
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
