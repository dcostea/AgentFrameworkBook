using Microsoft.Extensions.AI;
using Plugins.Enums;
using System.ComponentModel;

namespace AITools;

public class SensorTools
{
  private const int Delay = 500; // x milliseconds delay for mocking an action

  [/*KernelFunction("read_temperature"), */Description("Use thermal sensors to detect abnormal heat levels.")]
  public static async Task<int> ReadTemperatureAsync()
  {
    var random = new Random();
    var temperature = random.Next(-20, 100); // Simulate temperature reading
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] SENSORS: READING Temperature: {temperature} Celsius degrees.");
    await Task.Delay(Delay);
    return temperature;
  }

  [/*KernelFunction("read_infrared_radiation"), */Description("Confirm the presence of flames via IR sensors.")]
  public static async Task<int> ReadInfraredRadiationAsync()
  {
    var random = new Random();
    var irLevel = random.Next(0, 100); // Simulate IR reading
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] SENSORS: READING Infrared Radiation: {irLevel}");
    await Task.Delay(Delay);
    return irLevel;
  }

  [/*KernelFunction("read_humidity"), */Description("Check local humidity as a precursor to rain detection (fires often reduce local moisture).")]
  public static async Task<int> ReadHumidityAsync()
  {
    var random = new Random();
    var humidity = random.Next(0, 100); // Simulate humidity reading
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] SENSORS: READING Humidity: {humidity} %");
    await Task.Delay(Delay);
    return humidity;
  }

  //[KernelFunction("read_distance_to_object"), Description("Use ultrasonic sensors to measure the distance to object and ensure safe retreat.")]
  //public static async Task<int> ReadDistanceToObjectAsync()
  //{
  //    var random = new Random();
  //    var distance = random.Next(0, 100); // Simulate distance
  //    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] SENSORS: READING Distance to Object: {distance} meters");
  //    await Task.Delay(Delay);
  //    return distance;
  //}

  [/*KernelFunction("read_droplet_level"), */Description("Use optical or capacitive rain sensors to measure the presence and intensity of raindrops on surfaces like windshields or body panels.")]
  public static async Task<DropletLevel> ReadDropletLevelAsync()
  {
    var random = new Random();
    var values = Enum.GetValues<DropletLevel>();
    var dropletLevel = (DropletLevel)values.GetValue(random.Next(values.Length))!; // Simulate droplet level reading
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] SENSORS: READING Droplet Level: {dropletLevel}");
    await Task.Delay(Delay);
    return dropletLevel;
  }

  [/*KernelFunction("read_wind_speed"), */Description("Reads and returns the wind speed in kmph.")]
  public static async Task<int> ReadWindSpeedAsync()
  {
    var random = new Random();
    var speed = random.Next(0, 100);
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] SENSORS: READING Wind speed: {speed} kmph"); // Simulate wind speed reading
    await Task.Delay(Delay);
    return speed;
  }

  [/*KernelFunction("read_wind_direction"), */Description("Reads and returns the wind direction. The wind direction (output) is like North, NorthWest, etc.")]
  public static async Task<Direction> ReadWindDirectionAsync()
  {
    var random = new Random();
    var values = Enum.GetValues<Direction>();
    var direction = (Direction)values.GetValue(random.Next(values.Length))!;
    Console.WriteLine($"[{DateTime.Now:hh:mm:ss:fff}] SENSORS: READING Wind direction: {direction}"); // Simulate wind direction reading
    await Task.Delay(Delay);
    return direction;
  }

  static public IEnumerable<AITool> AsAITools()
  {
    yield return AIFunctionFactory.Create(ReadDropletLevelAsync);
    yield return AIFunctionFactory.Create(ReadHumidityAsync);
    yield return AIFunctionFactory.Create(ReadInfraredRadiationAsync);
    yield return AIFunctionFactory.Create(ReadTemperatureAsync);
    yield return AIFunctionFactory.Create(ReadWindDirectionAsync);
    yield return AIFunctionFactory.Create(ReadWindSpeedAsync);
    //yield return AIFunctionFactory.Create(ReadDistanceToObjectAsync);
  }
}
