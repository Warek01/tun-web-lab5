using System.Globalization;
using System.IO.Compression;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Timer = System.Threading.Timer;

namespace Go2Web.Models;

public class HttpRequest : IAsyncDisposable {
  // Config
  public int      MaxRedirects   { get; set; } = 10;
  public int      RequestTimeout { get; set; } = 10000;
  public Encoding AcceptEncoding { get; set; } = Encoding.UTF8;
  public bool     LogHeaders     { get; set; } = false;

  // Response
  public Dictionary<string, string>? Headers       { get; private set; }
  public HttpStatus?                 Status        { get; private set; }
  public int?                        StatusCode    { get; private set; }
  public string?                     StatusMessage { get; private set; }
  public string?                     Version       { get; private set; }
  public string?                     Body          { get; private set; }
  public Uri                         Uri           { get; private set; }

  private const int  MaxHeaderLineLength = 8196;
  private const byte Cr                  = (byte)'\r';
  private const byte Lf                  = (byte)'\n';

  private readonly Regex _requestLineRegex =
    new Regex(@"HTTP/(?<version>.+?) (?<code>\d+?) (?<message>.+)");

  private string?        _requestLine;
  private Timer?         _timer;
  private SslStream?     _sslStream;
  private NetworkStream? _networkStream;
  private Socket?        _socket;

  public HttpRequest(Uri uri) {
    Uri     = uri;
    Headers = new Dictionary<string, string>();
  }

  public async ValueTask DisposeAsync() {
    if (_sslStream != null)
      await _sslStream.DisposeAsync();

    if (_networkStream != null)
      await _networkStream.DisposeAsync();

    if (_socket != null) {
      await _socket.DisconnectAsync(true);
      _socket.Close();
      _socket.Dispose();
    }
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
  public async Task Fetch(bool followRedirects) {
    await Fetch();

    if (followRedirects && Status == HttpStatus.Redirect) {
      int redirectsCount = MaxRedirects;

      while (redirectsCount > 0 && Status == HttpStatus.Redirect) {
        Utils.LogInfo($"Redirect: {Uri} -> {Headers!["location"]}");
        redirectsCount--;

        Uri = new Uri(Headers["location"]);

        Headers       = null;
        StatusCode    = null;
        _requestLine  = null;
        Status        = null;
        Body          = null;
        StatusMessage = null;
        Version       = null;

        await Fetch();
      }

      if (redirectsCount == 0)
        throw new Exception($"Reached max redirect count ({MaxRedirects})");
    }
  }

  /// <summary>
  /// Request given URI, does not follow redirects.
  /// </summary>
  public async Task Fetch() {
    var tcs = new TaskCompletionSource<bool>();

    _timer = new Timer(
      _ => tcs.TrySetResult(true),
      null,
      RequestTimeout,
      Timeout.Infinite
    );

    Task responseTask   = DoRequest();
    int  firstCompleted = Task.WaitAny(new Task[] { tcs.Task, responseTask });

    if (firstCompleted == 0) {
      throw new TimeoutException("Request timed out");
    }

    await _timer.DisposeAsync();
  }

  private async Task DoRequest() {
    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    Utils.LogInfo($"Connecting to {Uri}...");
    await _socket.ConnectAsync(Uri.Host, Uri.Port);

    _networkStream = new NetworkStream(_socket);
    _sslStream     = new SslStream(_networkStream);

    await _sslStream.AuthenticateAsClientAsync(Uri.Host);
    byte[] requestBytes = GetEncodedRequestString();
    await _sslStream.WriteAsync(requestBytes);

    ExtractRequestLine();
    ExtractHeaders();
    ExtractBody();
  }

  private string ExtractOneLine() {
    int  position = 0;
    var  buffer   = new byte[MaxHeaderLineLength];
    byte prevByte = 0;

    while (position < MaxHeaderLineLength) {
      int readInt = _sslStream!.ReadByte();

      if (readInt == -1)
        throw new Exception("Reached end of stream");

      var currentByte = (byte)readInt;

      if (currentByte == Lf && prevByte == Cr) {
        return AcceptEncoding.GetString(
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

  private void ExtractRequestLine() {
    _requestLine = ExtractOneLine();
    Match match = _requestLineRegex.Match(_requestLine);

    int.TryParse(
      match.Groups["code"].Value,
      NumberStyles.Integer,
      CultureInfo.InvariantCulture,
      out int code
    );

    StatusCode    = code;
    Version       = match.Groups["version"].Value.Trim();
    StatusMessage = match.Groups["message"].Value.Trim();

    Status = StatusCode switch {
      >= 100 and <= 199 => HttpStatus.Informational,
      <= 299            => HttpStatus.Success,
      <= 399            => HttpStatus.Redirect,
      <= 499            => HttpStatus.ClientError,
      <= 599            => HttpStatus.ServerError,
      _                 => HttpStatus.Custom
    };
  }

  /// <summary>
  /// Extract headers from stream in lowercase
  /// </summary>
  private void ExtractHeaders() {
    Headers = new Dictionary<string, string>();

    while (true) {
      string line = ExtractOneLine();

      if (line.Length == 0) return;

      var    parts = line.ToLower().Split(":");
      string key   = parts[0].Trim();
      string value = string.Join(':', parts[1..]).Trim();

      if (LogHeaders) 
        Utils.LogKeyValuePair(key, value);

      Headers![key] = value;
    }
  }

  private void ExtractBody() {
    MemoryStream buffer;

    if (Headers!.TryGetValue("transfer-encoding", out string? value) && value == "chunked")
      buffer = ReadBodyChunked();
    else if (Headers!.ContainsKey("content-length"))
      buffer = ReadBodyByContentLength();
    else
      throw new Exception("Ambiguous transfer encoding");

    Headers!.TryGetValue("content-encoding", out string? rawEncoding);

    if (rawEncoding == null) {
      Body = Decompress(buffer, CompressionMethod.Identity);
      return;
    }

    CompressionMethod compressionMethod = rawEncoding.ToLower() switch {
      "br"       => CompressionMethod.Brotli,
      "deflate"  => CompressionMethod.Deflate,
      "identity" => CompressionMethod.Identity,
      "gzip"     => CompressionMethod.Gzip,
      _          => throw new Exception("Unknown compression method")
    };

    Body = Decompress(buffer, compressionMethod);
  }

  private MemoryStream ReadBodyChunked() {
    Utils.LogInfo("Reading body by chunks...");

    int chunksCount      = 0;
    var buffer           = new MemoryStream();
    int currentChunkSize = 0;

    while (true) {
      if (currentChunkSize == 0) {
        string line = ExtractOneLine();

        if (line.Length == 0)
          line = ExtractOneLine();

        currentChunkSize = int.Parse(line, NumberStyles.HexNumber);

        if (currentChunkSize == 0) break;

        chunksCount++;
      }

      int readInt = _sslStream!.ReadByte();

      if (readInt == -1)
        throw new Exception("Reached end of stream");

      var b = (byte)readInt;

      buffer.WriteByte(b);
      currentChunkSize--;
    }

    Utils.LogInfo($"Received {chunksCount} chunks...");

    buffer.Seek(0, 0);
    return buffer;
  }

  private MemoryStream ReadBodyByContentLength() {
    Utils.LogInfo("Reading body by Content-Length...");

    int remaining = int.Parse(Headers!["content-length"]);
    var buffer    = new MemoryStream(remaining);

    do {
      int readInt = _sslStream!.ReadByte();

      if (readInt == -1)
        throw new Exception("Reached end of stream");

      var b = (byte)readInt;
      buffer.WriteByte(b);
      remaining--;
    } while (remaining > 0);

    buffer.Seek(0, 0);
    return buffer;
  }

  private byte[] GetEncodedRequestString() {
    return AcceptEncoding.GetBytes(
      $"""
       GET {Uri.PathAndQuery} HTTP/1.1
       Host: {Uri.Host}
       Connection: keep-alive
       Accept: text/html, application/json; charset=utf-8
       User-Agent: go2web client
       Accept-Encoding: gzip, br, deflate, identity
       Accept-Language: en-US
       Cache-Control: no-cache; max-age=0


       """
    );
  }

  private string Decompress(MemoryStream stream, CompressionMethod method) {
    Utils.LogInfo($"Decompressing from {method}...");

    using Stream compressionStream = method switch {
      CompressionMethod.Gzip     => new GZipStream(stream, CompressionMode.Decompress),
      CompressionMethod.Deflate  => new DeflateStream(stream, CompressionMode.Decompress),
      CompressionMethod.Brotli   => new BrotliStream(stream, CompressionMode.Decompress),
      CompressionMethod.Identity => stream,
      _                          => throw new Exception("Unknown compression method")
    };

    using var reader = new StreamReader(compressionStream, AcceptEncoding);
    return reader.ReadToEnd();
  }
}
