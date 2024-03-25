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
  s.CaseSensitive          = true;
  s.IgnoreUnknownArguments = true;
  s.ParsingCulture         = CultureInfo.InvariantCulture;
});
var  options     = Parser.Default.ParseArguments<CliOptions>(args).Value;
var  config      = Config.Read("Config.json");
var  cache       = new HttpCache("Cache");
bool showDivider = false;
bool ignoreCache = false;

if (options.Help) {
  Console.WriteLine(CliOptions.HelpString);
  showDivider = true;
}

if (options.Headers) {
  Utils.LogWarning("Ignoring cache to read headers ...");
  ignoreCache = true;
}

if (options.ClearCache) {
  if (showDivider) Utils.LogDivider();
  showDivider = true;

  int clearedCount = cache.Clear();
  Utils.LogWarning($"Cleared {clearedCount} cache {(clearedCount == 1 ? "entry" : "entries")}");
}

if (options.Url != null) {
  if (showDivider) Utils.LogDivider();
  showDivider = true;

  var    uri         = HttpRequest.UrlToUri(options.Url);
  string httpContent = await DoRequest(uri);

  if (httpContent.StartsWith('{') && httpContent.EndsWith('}')) {
    Console.WriteLine("JSON:");
    Console.WriteLine(JsonHelper.Format(httpContent));
  } else {
    var page = new HtmlPage(httpContent, uri);

    await page.Init();
    Console.Write(page.GetTextContent());
  }
}

if (options.Search != null && options.Search.Any()) {
  if (showDivider) Utils.LogDivider();

  var    term        = string.Join(' ', options.Search);
  var    uri         = new Uri("https://google.com/search?q=" + Uri.EscapeDataString(term));
  string httpContent = await DoRequest(uri);
  var    page        = new HtmlPage(httpContent, uri);

  await page.Init();

  string content = page.GetSearchResults();

  Console.ForegroundColor = ConsoleColor.Green;
  Console.WriteLine($"Search results for \"{term}\":");
  Console.ResetColor();
  Console.WriteLine(content);
}

return;

async Task<string> DoRequest(Uri uri) {
  var request = new HttpRequest(uri) {
    MaxRedirects   = config.MaxRedirects,
    RequestTimeout = config.RequestTimeout,
    LogHeaders     = options.Headers,
  };

  string? content = ignoreCache ? null : cache.Get(uri);

  if (content == null) {
    await request.Fetch(true);
    content = request.Body;
    cache.Add(uri, content!);
  } else {
    Console.WriteLine("Found URI in cache ...");
  }

  return content!;
}
