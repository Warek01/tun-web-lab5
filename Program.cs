using System.Globalization;
using System.Text;
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

  var      uri  = HttpRequest.UrlToUri(options.Url);
  HtmlPage page = await GetPage(uri);
  Console.Write(page.GetTextContent());
}

if (options.Search != null) {
  if (showDivider) Utils.LogDivider();

  var      term    = string.Join(' ', options.Search);
  var      uri     = new Uri("https://google.com/search?q=" + Uri.EscapeDataString(term));
  HtmlPage page    = await GetPage(uri);
  string   content = page.GetSearchResults();

  Console.ForegroundColor = ConsoleColor.Green;
  Console.WriteLine($"Search results for \"{term}\":");
  Console.ResetColor();
  Console.WriteLine(content);
}

return;

async Task<HtmlPage> GetPage(Uri uri) {
  var request = new HttpRequest(uri) {
    MaxRedirects   = config.MaxRedirects,
    RequestTimeout = config.RequestTimeout
  };

  string? content = cache.Get(uri);

  if (content == null) {
    await request.Fetch(true);
    content = request.Body;
    cache.Add(uri, content!);
  } else {
    Console.WriteLine("Found URI in cache ...");
  }

  var page = new HtmlPage(content!, uri);
  await page.Init();

  return page;
}
