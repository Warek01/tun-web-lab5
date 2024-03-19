using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CommandLine;
using TumWebLab5.Models;

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
} else if (options.Url != null) {
  var http = new HttpModule();

  var message = http.Get(options.Url);
  Console.WriteLine(message);
} else if (options.Search != null) {
  throw new NotImplementedException();
}
