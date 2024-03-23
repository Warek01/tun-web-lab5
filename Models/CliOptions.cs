using CommandLine;

namespace Go2Web.Models;

public class CliOptions {
  [Option('u', "url", Required = false)]
  public string? Url { get; set; } = null;

  [Option('s', "search", Required = false)]
  public IEnumerable<string>? Search { get; set; } = null;

  [Option('h', "help", Required = false)]
  public bool Help { get; set; } = false;

  [Option('c', "clear-cache", Required = false)]
  public bool ClearCache { get; set; } = false;
}
