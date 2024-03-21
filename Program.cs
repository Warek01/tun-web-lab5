using System.Globalization;
using System.Text;
using CommandLine;
using Go2Web.Models;

Thread.CurrentThread.CurrentCulture   = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

using var cliParser = new Parser(s => {
  s.AutoHelp               = false;
  s.AutoVersion            = false;
  s.CaseSensitive          = false;
  s.IgnoreUnknownArguments = true;
  s.ParsingCulture         = CultureInfo.InvariantCulture;
});
var options = Parser.Default.ParseArguments<CliOptions>(args).Value;

Config.GlobalEncoding = Encoding.UTF8;
var config = Config.Read("Config.json");
var cache  = new HttpCache("Cache");
var http   = new HttpModule(config);

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
  
  var uri = HttpModule.UrlToUri(options.Url);   
  string? content = cache.Get(uri);

  if (content == null) {
    content = await http.RequestPage(uri);
    if (content == null) return;
    Console.Write(content);
    cache.Add(uri, content);
  }
  else {
    Console.WriteLine(content);
  }
}

if (options.Search != null) {
  if (options.Help || options.Url != null) Utils.LogDivider();
  var uri = HttpModule.UrlToUri(options.Search);
  throw new NotImplementedException();
}
