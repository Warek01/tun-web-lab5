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

  [Option('H', "headers", Required = false)]
  public bool Headers { get; set; } = false;

  public const string HelpString =
    """
    go2web - perform a web HTTP request

    Options:
      -u --url <URL>                     Make an HTTP request to the specified URL and print the response
    
      -s --search <search-term>          Make an HTTP request to search the term using your favorite search engine and print top 10 results
    
      -h --help                          Show this help
    
      -c --clear-cache                   Clears the cache
      
      -H --headers                       Show response headers
    """;
}
