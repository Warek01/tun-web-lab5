using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace TumWebLab5.Models;

public static partial class HttpModule {
  public static HttpMessage? Get(string url) {
    Regex httpRegex = GetHttpRegex();

    if (!httpRegex.IsMatch(url))
      url = "https://" + url;

    var uri = new Uri(url);

    return Get(uri);
  }

  public static HttpMessage? Get(Uri uri) {
    return Request(uri);
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
    return Encoding.UTF8.GetBytes(
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
    using var memStream  = new MemoryStream(Encoding.UTF8.GetBytes(message));
    using var gzipStream = new GZipStream(memStream, CompressionMode.Decompress);
    using var reader     = new StreamReader(gzipStream, Encoding.UTF8);
    return reader.ReadToEnd();
  }

  [GeneratedRegex(
    "^HTTPS?://",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
  )]
  private static partial Regex GetHttpRegex();
}
