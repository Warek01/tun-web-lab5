using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace TumWebLab5.Models;

public class HttpModule {
  private readonly HttpCache _cache = new HttpCache();

  public HttpModule() { }

  public string Get(string url) {
    try {
      var socket  = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
      var address = GetHostAddress(url);

      if (address == null)
        throw new Exception($"Failed to resolve {url}");

      var ep = new IPEndPoint(address, 80);

      socket.Connect(ep);

      var buffer = new byte[1024 * 10];

      socket.Receive(buffer);

      Console.WriteLine(Encoding.UTF8.GetString(buffer));

      socket.Close();
    } catch (Exception ex) {
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine($"Error requesting {url}");
      Console.ResetColor();
      Console.WriteLine();
      Console.WriteLine(ex);
    }

    return string.Empty;
  }

  private IPAddress? GetHostAddress(string url) {
    var regex = new Regex("^https?://");

    if (regex.Match(url).Success)
      url = regex.Replace(url, string.Empty);

    var hostInfo = Dns.GetHostEntry(url);

    return hostInfo.AddressList.FirstOrDefault();
  }
}
