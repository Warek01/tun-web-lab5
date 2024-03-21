using System.Globalization;
using CommandLine;
using TumWebLab5.Models;

Thread.CurrentThread.CurrentCulture   = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

using var cliParser = new Parser(s => {
  s.AutoHelp               = false;
  s.AutoVersion            = false;
  s.CaseSensitive          = false;
  s.IgnoreUnknownArguments = true;
  s.ParsingCulture         = CultureInfo.InvariantCulture;
});
var config  = Config.Read("Config.json");
var options = Parser.Default.ParseArguments<CliOptions>(args).Value;

if (options.Help) {
  Console.WriteLine(
    """
    go2web -u --url <URL>            Make an HTTP request to the specified URL and print the response
    go2web -s --search <search-term> Make an HTTP request to search the term using your favorite search engine and print top 10 results
    go2web -h --help                 Show this help
    """
  );
}

if (options.Url != null) {
  if (options.Help) Utils.LogDivider();
  RequestUrl(options.Url);
}

if (options.Search != null) {
  if (options.Help || options.Url != null) Utils.LogDivider();
  throw new NotImplementedException();
}

return;

void RequestUrl(string url) {
  HttpMessage? message = HttpModule.Get(url);

  if (message == null) {
    Utils.LogError("Error parsing response");
    return;
  }

  Console.WriteLine(message);

  if (message.ResponseType == HttpResponseType.Redirect) {
    int redirectsCount = config.MaxRedirects;

    while (redirectsCount-- > 0) {
      Console.WriteLine($"Redirect: {url} -> {message.Headers["Location"]}");
      url     = message.Headers["Location"];
      message = HttpModule.Get(url);

      if (message == null) {
        Utils.LogError("Error parsing response");
        return;
      }

      if (message.ResponseType == HttpResponseType.Ok) {
        Console.WriteLine(message);
        return;
      }
    }

    Console.WriteLine($"Reached max redirect count ({config.MaxRedirects})");
    return;
  }

  Console.WriteLine(message);
}
