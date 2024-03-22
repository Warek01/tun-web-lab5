using System.Globalization;
using System.Text;
using AngleSharp.Io;
using CommandLine;
using Go2Web.Models;

Thread.CurrentThread.CurrentCulture   = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
Config.GlobalEncoding                 = Encoding.UTF8;

using var cliParser = new Parser(s => {
  s.AutoHelp               = false;
  s.AutoVersion            = false;
  s.CaseSensitive          = false;
  s.IgnoreUnknownArguments = true;
  s.ParsingCulture         = CultureInfo.InvariantCulture;
});
var  options     = Parser.Default.ParseArguments<CliOptions>(args).Value;
var  config      = Config.Read("Config.json");
var  cache       = new HttpCache("Cache");
bool showDivider = false;

if (options.Help) {
  Console.WriteLine(
    """
    go2web - perform a web HTTP request

    Options:
      -u --url <URL>          Make an HTTP request to the specified URL and print the response
    
      -s --search <search-term>          Make an HTTP request to search the term using your favorite search engine and print top 10 results
    
      -h --help          Show this help
    
      -c --clear-cache          Clears the cache
    """
  );

  showDivider = true;
}

if (options.ClearCache) {
  if (showDivider) Utils.LogDivider();
  showDivider = true;

  int clearedCount = cache.Clear();
  Console.WriteLine($"Cleared {clearedCount} cache entr{(clearedCount == 1 ? "y" : "ies")}");
}

if (options.Url != null) {
  if (showDivider) Utils.LogDivider();
  showDivider = true;

  var     uri     = HttpRequest.UrlToUri(options.Url);
  string? content = cache.Get(uri);

  if (content == null) {
    string rawHtml = await GetRawHtml(uri);
    var    page    = new HtmlPage(rawHtml, uri);

    await page.Init();
    content = page.GetTextContent();

    Console.Write(content);
    cache.Add(uri, content);
  } else {
    Console.WriteLine(content);
  }
}

if (options.Search != null) {
  if (showDivider) Utils.LogDivider();

  var    uri     = new Uri("https://google.com/search?q=" + Uri.EscapeDataString(options.Search));
  string rawHtml = await GetRawHtml(uri);
}

return;

async Task<string> GetRawHtml(Uri uri) {
  var request = new HttpRequest(uri) {
    MaxRedirects   = config!.MaxRedirects,
    RequestTimeout = config!.RequestTimeout
  };

  return await request.AsHtml();
}
