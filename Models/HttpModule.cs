using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace TumWebLab5.Models;

public class HttpModule {
  // private readonly HttpCache _cache = new HttpCache();

  private const string RequestLine = "GET / HTTP/1.1";

  public HttpMessage Get(string url) {
    var regex = new Regex("^HTTPS?://", RegexOptions.IgnoreCase);

    if (!regex.IsMatch(url))
      url = "https://" + url;

    return Get(new Uri(url));
  }

  public HttpMessage? Get(Uri uri) {
    try {
      var content = Request(uri);
      return new HttpMessage(content);
    } catch (Exception ex) {
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine($"Error requesting {uri.Host}");
      Console.ResetColor();
      Console.WriteLine();
      Console.WriteLine(ex);

      return null;
    }
  }

  private string Request(Uri uri) {
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    var headers =
      $"""
       Host: {uri.Host}
       Connection: keep-alive
       Accept: text/html
       User-Agent: CSharpTests
       """;
    var request = $"""
                   {RequestLine}
                   {headers}


                   """;
    var buffer = new byte[1024];

    socket.Connect(uri.Host, 80);
    socket.Send(Encoding.UTF8.GetBytes(request));
    socket.Receive(buffer);
    socket.Close();

    return Encoding.UTF8.GetString(buffer);
  }
}
