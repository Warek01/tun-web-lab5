using System.Globalization;
using System.IO.Compression;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Timer = System.Threading.Timer;

namespace Go2Web.Models.Http;

public class Request : IAsyncDisposable {
  // Config
  public int                        MaxRedirects   { get; set; } = 10;
  public int                        RequestTimeout { get; set; } = 10000;
  public bool                       LogHeaders     { get; set; }
  public Dictionary<string, string> Headers        { get; }

  public Response? Response { get; private set; }
  public Uri       Uri      { get; private set; }

  private const int  MaxHeaderLineLength = 8196;
  private const byte Cr                  = (byte)'\r';
  private const byte Lf                  = (byte)'\n';
  private const int  SecurePort          = 443;

  private readonly Regex _requestLineRegex =
    new Regex(@"^HTTP/(?<Version>.+?)\s(?<Code>\d+?)\s(?<Message>.+)$");

  private string?        _requestLine;
  private Timer?         _timer;
  private Stream?        _receiveStream;
  private SslStream?     _sslStream;
  private NetworkStream? _networkStream;
  private Socket?        _socket;

  public Request(string url) : this(UrlToUri(url)) { }

  public Request(Uri uri) {
    Uri = uri;

    Headers = new Dictionary<string, string> {
      ["Connection"]      = "keep-alive",
      ["User-Agent"]      = "go2web client",
      ["Accept"]          = "text/html, application/json; charset=utf-8",
      ["Accept-Encoding"] = "gzip, br, deflate, identity",
      ["Accept-Language"] = "en-US",
      ["Cache-Control"]   = "no-cache; max-age=0",
      ["Content-Length"]  = "0",
      ["Cookie"]          = "",
      ["DNT"]             = "1",
    };
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

    if (_timer != null)
      await _timer.DisposeAsync();
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

    if (followRedirects && Response!.Status == Status.Redirect) {
      int redirectsCount = MaxRedirects;

      while (redirectsCount > 0 && Response!.Status == Status.Redirect) {
        string location = Response.Headers["Location"];

        Utils.LogInfo($"Redirect: {Uri} -> {location}");
        redirectsCount--;
        Uri      = new Uri(location);
        Response = null;

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
    Response = new Response();
    _timer = new Timer(
      _ => tcs.TrySetResult(true),
      null,
      RequestTimeout,
      Timeout.Infinite
    );
    Task responseTask   = DoRequest();
    int  firstCompleted = Task.WaitAny(new Task[] { tcs.Task, responseTask });

    if (firstCompleted == 0)
      throw new TimeoutException("Request timed out");

    await _timer.DisposeAsync();
  }

  private async Task DoRequest() {
    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    Utils.LogInfo($"Connecting to {Uri}...");
    await _socket.ConnectAsync(Uri.Host, Uri.Port);

    if (!_socket.Connected)
      throw new Exception($"Could not connect to {Uri}");

    _receiveStream = _networkStream = new NetworkStream(_socket, FileAccess.ReadWrite, false);
    byte[] requestBytes             = GetEncodedRequestString();

    if (Uri.Port == SecurePort) {
      _sslStream = new SslStream(_networkStream);
      await _sslStream.AuthenticateAsClientAsync(Uri.Host);
      await _sslStream.WriteAsync(requestBytes);
      _receiveStream = _sslStream;
    }

    await _receiveStream.WriteAsync(requestBytes);

    ExtractRequestLine();
    ExtractHeaders();
    ExtractBody();
  }

  private string ExtractOneLine() {
    int  position = 0;
    var  buffer   = new byte[MaxHeaderLineLength];
    byte prevByte = 0;

    while (position < MaxHeaderLineLength) {
      int readInt = _receiveStream!.ReadByte();

      if (readInt == -1)
        throw new Exception("Reached end of stream");

      var currentByte = (byte)readInt;

      if (currentByte == Lf && prevByte == Cr)
        return Encoding.UTF8.GetString(
          buffer,
          0,
          position - 1 // Ignore last byte because its CR
        );

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
      match.Groups["Code"].Value,
      NumberStyles.Integer,
      CultureInfo.InvariantCulture,
      out int code
    );

    Response!.StatusCode    = code;
    Response!.Version       = match.Groups["Version"].Value.Trim();
    Response!.StatusMessage = match.Groups["Message"].Value.Trim();

    Response!.Status = Response!.StatusCode switch {
      >= 100 and <= 199 => Status.Informational,
      <= 299            => Status.Success,
      <= 399            => Status.Redirect,
      <= 499            => Status.ClientError,
      <= 599            => Status.ServerError,
      _                 => Status.Custom
    };
  }

  /// <summary>
  /// Extract headers from stream in lowercase
  /// </summary>
  private void ExtractHeaders() {
    while (true) {
      string line = ExtractOneLine();

      if (line.Length == 0) return;

      var    parts = line.Split(":");
      string key   = parts[0].Trim();
      string value = string.Join(':', parts[1..]).Trim();

      if (LogHeaders)
        Utils.LogKeyValuePair(key, value);

      Response!.Headers[key] = value;
    }
  }

  private void ExtractBody() {
    MemoryStream buffer;

    if (Response!.Headers.TryGetValue("Transfer-Encoding", out string? value) && value == "chunked")
      buffer = ReadBodyChunked();
    else if (Response!.Headers.ContainsKey("Content-Length"))
      buffer = ReadBodyByContentLength();
    else
      throw new Exception("Ambiguous transfer encoding");

    Response!.Headers.TryGetValue("Content-Encoding", out string? rawEncoding);

    if (rawEncoding == null) {
      Response!.Body = Decompress(buffer, CompressionMethod.Identity);
      return;
    }

    CompressionMethod compressionMethod = rawEncoding switch {
      "br"       => CompressionMethod.Brotli,
      "deflate"  => CompressionMethod.Deflate,
      "identity" => CompressionMethod.Identity,
      "gzip"     => CompressionMethod.Gzip,
      _          => throw new Exception("Unknown compression method")
    };

    Response!.Body = Decompress(buffer, compressionMethod);
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

      int readInt = _receiveStream!.ReadByte();

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

    int remaining = int.Parse(Response!.Headers["Content-Length"]);
    var buffer    = new MemoryStream(remaining);

    do {
      int readInt = _receiveStream!.ReadByte();

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
    var sb = new StringBuilder();
    sb.Append("GET ").Append(Uri.PathAndQuery).AppendLine(" HTTP/1.1");
    sb.Append("Host: ").AppendLine(Uri.Host);

    foreach (var (key, value) in Headers)
      sb.Append(key).Append(": ").AppendLine(value);

    sb.AppendLine();
    
    return Encoding.UTF8.GetBytes(sb.ToString());
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

    using var reader = new StreamReader(compressionStream, Encoding.UTF8);
    return reader.ReadToEnd();
  }
}
