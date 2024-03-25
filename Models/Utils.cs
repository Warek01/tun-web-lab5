namespace Go2Web.Models;

public static class Utils {
  public static void LogError(string text) {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {text}");
    Console.ResetColor();
  }

  public static void LogError(string text, Exception ex) {
    LogError(text);
    Console.BackgroundColor = ConsoleColor.DarkRed;
    Console.WriteLine(ex);
    Console.ResetColor();
  }

  public static void LogWarning(string text) {
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(text);
    Console.ResetColor();
  }

  public static void LogKeyValuePair(string key, string value) {
    Console.ForegroundColor = ConsoleColor.DarkBlue;
    Console.Write(key);
    Console.ResetColor();
    Console.WriteLine($": {value}");
  }

  public static void LogInfo(string text) {
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(text);
    Console.ResetColor();
  }

  public static void LogDivider() {
    Console.WriteLine("--------------------");
  }
}
