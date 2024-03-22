using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Go2Web.Models;

public partial class HttpResponse {
  public HttpStatus                 Status        { get; set; }
  public int                        StatusCode    { get; set; }
  public string                     StatusMessage { get; set; }
  public Dictionary<string, string> Headers       { get; set; }
  public string                     Version       { get; set; }
  public string                     Body          { get; set; }
  public Uri                        Uri           { get; set; }

  private readonly Regex _requestLineRegex =
    new Regex(@"HTTP/(?<version>.+?) (?<code>\d+?) (?<message>.+)");

  public HttpResponse(
    string                     requestLine,
    Dictionary<string, string> headers,
    string                     body,
    Uri                        uri
  ) {
    Uri = uri;

    Match match = _requestLineRegex.Match(requestLine);

    int.TryParse(
      match.Groups["code"].Value,
      NumberStyles.Integer,
      CultureInfo.InvariantCulture,
      out int code
    );

    StatusCode    = code;
    Headers       = headers;
    Body          = body;
    Version       = match.Groups["version"].Value.Trim();
    StatusMessage = match.Groups["message"].Value.Trim();

    Status = StatusCode switch {
      >= 100 and <= 199 => HttpStatus.Progress,
      <= 299            => HttpStatus.Ok,
      <= 399            => HttpStatus.Redirect,
      <= 499            => HttpStatus.ClientError,
      <= 599            => HttpStatus.ServerError,
      _                 => HttpStatus.Custom
    };
  }

  public override string ToString() {
    var sb = new StringBuilder();

    sb.AppendLine($"Type: {Status}");
    sb.AppendLine($"Version: {Version}");
    sb.AppendLine($"Code: {StatusCode}");
    sb.AppendLine($"Message: {StatusMessage}");
    sb.AppendLine("Headers:");

    foreach (var header in Headers)
      sb.AppendLine($"{header.Key}: {header.Value}");

    sb.AppendLine("Body:");
    sb.AppendLine(Body);

    return sb.ToString();
  }
}
