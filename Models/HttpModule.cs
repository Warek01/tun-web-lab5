using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace TumWebLab5.Models;

public partial class HttpModule {
  private readonly Config _config;

  public HttpModule(Config config) {
    _config = config;
  }

  public static Uri UrlToUri(string url) {
    var httpRegex = new Regex(
      "^HTTPS?://",
      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    if (!httpRegex.IsMatch(url))
      url = "https://" + url;

    return new Uri(url);
  }

  public HttpMessage? Get(Uri uri) {
    return Request(uri);
  }

  public async Task<string?> RequestPage(Uri uri) {
    HttpMessage? message = Get(uri);

    if (message == null) 
      throw new Exception("Error parsing response");

    if (message.ResponseType == HttpResponseType.Redirect) {
      int redirectsCount = _config.MaxRedirects;

      while (redirectsCount-- > 0 && message.ResponseType != HttpResponseType.Ok) {
        Console.WriteLine($"Redirect: {uri} -> {message.Headers["Location"]}");
        uri     = new Uri(message.Headers["Location"]);
        message = Get(uri);

        if (message == null) 
          throw new Exception("Error parsing response");
      }

      if (redirectsCount == 0) {
        Console.WriteLine($"Reached max redirect count ({_config.MaxRedirects})");
        return null;
      }
    }

    var page = new HtmlPage(message.Body, message.Uri);
    await page.Init();
    return page.GetContent();
  }

  private static HttpMessage? Request(Uri uri) {
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    try {
      socket.Connect(uri.Host, uri.Port);

      using var networkStream = new NetworkStream(socket);
      using var sslStream     = new SslStream(networkStream);

      sslStream.AuthenticateAsClient(uri.Host);
      sslStream.Write(GetEncodedRequestString(uri));

      using var sr      = new StreamReader(sslStream, Encoding.ASCII);
      string    content = sr.ReadToEnd();

      sr.Close();
      sslStream.Close();
      networkStream.Close();

      var message = new HttpMessage(content, uri);

      if (message.Encoding == HttpEncoding.Gzip) {
        message.Body = DecompressFromGzip(message.Body);
      }

      return message;
    } catch (Exception ex) {
      Utils.LogError($"Error requesting {uri.Host}", ex);
      return null;
    } finally {
      socket.Disconnect(true);
      socket.Close();
    }

    // --- manual version:
    // using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    // socket.Connect(uri.Host, uri.Port);
    // socket.ReceiveBufferSize = ResponseBufferSize;
    // socket.Connect(uri.Host,  uri.Port);
    // socket.Send(GetRequestStringBytes(uri));
    //
    // var body = new StringBuilder();
    // body.EnsureCapacity(MinBodyStringBuilderSize);
    //
    // try {
    //   int bytesReceived;
    //
    //   do {
    //     var buffer = new byte[ResponseBufferSize];
    //     bytesReceived = socket.Receive(buffer, SocketFlags.None);
    //     string str = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
    //     body.Append(str);
    //   } while (bytesReceived >= ResponseBufferSize);
    // } catch (Exception ex) {
    //   Utils.LogError("Error receiving data", ex);
    // }
    //
    // socket.Disconnect(true);
    // socket.Close();
    //
    // return body.ToString();
  }

  private static byte[] GetEncodedRequestString(Uri uri) {
    return Config.GlobalEncoding.GetBytes(
      $"""
       GET {uri.PathAndQuery} HTTP/1.1
       Host: {uri.Host}
       Connection: close
       Accept: text/html, application/json; charset=utf-8
       User-Agent: go2web client
       Accept-Encoding: identity
       Accept-Language: en-US
       Cache-Control: no-cache; max-age=0


       """
    );
  }

  // TODO: fix "The archive entry was compressed using an unsupported compression method."
  private static string DecompressFromGzip(string message) {
    using var memStream  = new MemoryStream(Config.GlobalEncoding.GetBytes(message));
    using var gzipStream = new GZipStream(memStream, CompressionMode.Decompress);
    using var reader     = new StreamReader(gzipStream, Config.GlobalEncoding);
    return reader.ReadToEnd();
  }
}
