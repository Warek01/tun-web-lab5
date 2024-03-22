using System.IO.Compression;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Timer = System.Threading.Timer;

namespace Go2Web.Models;

public class HttpRequest {
  public int MaxRedirects   { get; set; } = 10;
  public int RequestTimeout { get; set; } = 10000;

  private const int  MaxHeaderLineLength = 8196;
  private const byte Cr                  = (byte)'\r';
  private const byte Lf                  = (byte)'\n';

  private Uri    _uri;
  private Timer? _timer;

  public HttpRequest(Uri uri) {
    _uri = uri;
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

  /// <summary>
  ///  Follows redirects and returns raw HTML
  /// </summary>
  /// <returns>Raw HTML</returns>
  public async Task<string> AsHtml() {
    HttpResponse message = await AsHttpResponse();

    if (message.Status == HttpStatus.Redirect) {
      int redirectsCount = MaxRedirects;

      while (
        redirectsCount  > 0
        && message.Status != HttpStatus.Ok
      ) {
        Console.WriteLine($"Redirect: {_uri} -> {message.Headers["Location"]}");
        redirectsCount--;

        _uri    = new Uri(message.Headers["Location"]);
        message = await AsHttpResponse();
      }

      if (redirectsCount == 0)
        throw new Exception($"Reached max redirect count ({MaxRedirects})");
    }

    return message.Body;
  }

  /// <summary>
  /// Request given URI, does not follow redirects.
  /// </summary>
  public async Task<HttpResponse> AsHttpResponse() {
    var tcs = new TaskCompletionSource<bool>();

    _timer = new Timer(
      _ => tcs.TrySetResult(true),
      null,
      RequestTimeout,
      Timeout.Infinite
    );

    Task<HttpResponse> responseTask   = DoRequest();
    int                firstCompleted = Task.WaitAny(new Task[] { tcs.Task, responseTask });

    if (firstCompleted == 0) {
      throw new TimeoutException("Request timed out");
    }

    await _timer.DisposeAsync();
    return responseTask.Result;
  }

  private async Task<HttpResponse> DoRequest() {
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    await socket.ConnectAsync(_uri.Host, _uri.Port);

    await using var networkStream = new NetworkStream(socket);
    await using var sslStream     = new SslStream(networkStream);

    await sslStream.AuthenticateAsClientAsync(_uri.Host);
    sslStream.Write(GetEncodedRequestString(_uri));

    string                     requestLine   = ExtractOneLine(sslStream);
    Dictionary<string, string> headers       = ExtractHeaders(sslStream);
    int                        contentLength = int.Parse(headers["content-length"]);
    string                     body          = ExtractBody(sslStream, contentLength);

    return new HttpResponse(requestLine, headers, body, _uri);
  }

  private static string ExtractOneLine(Stream stream) {
    int  position = 0;
    var  buffer   = new byte[MaxHeaderLineLength];
    byte prevByte = 0;

    while (position < MaxHeaderLineLength) {
      int readInt = stream.ReadByte();

      if (readInt == -1)
        throw new Exception("Reached end of stream");

      var currentByte = (byte)readInt;

      if (currentByte == Lf && prevByte == Cr) {
        return Config.GlobalEncoding.GetString(
          buffer,
          0,
          position - 1 // Ignore last byte because its CR
        );
      }

      prevByte         = currentByte;
      buffer[position] = currentByte;
      position++;
    }

    throw new Exception("Max size of header line reached");
  }

  /// <summary>
  /// Extract headers from stream in lowercase
  /// </summary>
  private static Dictionary<string, string> ExtractHeaders(Stream stream) {
    var headers     = new Dictionary<string, string>();
    var headerRegex = new Regex(@"(?<key>[\w\-;, \t/\.+()=*#$]+):[ \t]*?(?<value>.+)\r?\n");

    while (true) {
      string line = ExtractOneLine(stream);


      if (line.Length == 0) return headers;

      var    parts = line.ToLower().Split(":");
      string key   = parts[0];
      string value = parts[1];

      headers[key] = value;
    }
  }

  private static string ExtractBody(Stream stream, int contentLength) {
    var buffer    = new byte[contentLength];
    int index     = 0;

    do {
      index += stream.Read(buffer);
    } while (index < contentLength);

    return Config.GlobalEncoding.GetString(buffer);
  }

  private static byte[] GetEncodedRequestString(Uri uri) {
    return Config.GlobalEncoding.GetBytes(
      $"""
       GET {uri.PathAndQuery} HTTP/1.1
       Host: {uri.Host}
       Connection: keep-alive
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
