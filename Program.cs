using System.Net;
using System.Net.Sockets;
using System.Text;
using TumWebLab5.Models;

var url    = new CliParameter(args, "-u", true);
var search = new CliParameter(args, "-s", true);
var help   = new CliParameter(args, "-h", false);

if (help.Found) {
  Console.WriteLine(
    """
    go2web -u <URL>         # make an HTTP request to the specified URL and print the response
    go2web -s <search-term> # make an HTTP request to search the term using your favorite search engine and print top 10 results
    go2web -h               # show this help
    """
  );

} else if (url is { Found: true, Value: not null }) {
  var http = new HttpModule();

  var message = http.Get(url.Value);
  Console.WriteLine(message);
} else if (search is { Found: true, Value: not null }) {
  throw new NotImplementedException();
}
