namespace TumWebLab5.Models;

public class CliParameter {
  public string  Name      { get; }
  public string? Value     { get; private set; } = null;
  public bool    WithValue { get; }
  public bool    Found     { get; private set; } = false;

  public CliParameter(string[] args, string name, bool withValue = true) {
    Name      = name;
    WithValue = withValue;

    ParseArgs(args);
  }

  private void ParseArgs(string[] args) {
    for (int i = 0; i < args.Length; i++) {
      string arg = args[i];

      if (arg != Name) continue;
      Found = true;
      
      if (!WithValue || i == args.Length - 1) return;
      string next = args[i + 1];

      if (next.StartsWith('-')) continue;
      Value = args[i + 1];
    }
  }
}
