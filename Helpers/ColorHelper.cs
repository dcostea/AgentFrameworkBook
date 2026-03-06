namespace Helpers;

public static class ColorHelper
{
  public static void PrintColoredLine(string text, ConsoleColor color)
  {
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ResetColor();
  }

  public static void PrintColored(string text, ConsoleColor color)
  {
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ResetColor();
  }
}