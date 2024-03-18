namespace TumWebLab5.Models;

public static class Utils {
  public static void LogError(string text) {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {text}");
    Console.ResetColor();
  }

  public static void LogError(string text, Exception ex) {
    LogError(text);
    Console.BackgroundColor = ConsoleColor.DarkBlue;
    Console.WriteLine(ex);
    Console.ResetColor();
  }
}
