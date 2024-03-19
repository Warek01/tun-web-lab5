using CommandLine;

namespace TumWebLab5.Models;

public class CliOptions {
  [Option('u', "url", Required = false, HelpText = "Show page from URL")]
  public string? Url { get; set; } = null;

  [Option('s', "search", Required = false, HelpText = "Search with google")]
  public string? Search { get; set; } = null;
  
  [Option('h', "help", Required = false, HelpText = "Get help")]
  public bool Help { get; set; }
}
