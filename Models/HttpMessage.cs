using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TumWebLab5.Models;

public partial class HttpMessage {
  private static readonly Regex ParseRegex  = GetParseRegex();
  private static readonly Regex HeaderRegex = GetHeaderRegex();

  public HttpRequestType            RequestType  { get; set; }
  public HttpResponseType           ResponseType { get; set; }
  public string                     RawHeaders   { get; set; }
  public Dictionary<string, string> Headers      { get; set; }
  public string                     Version      { get; set; }
  public int                        Code         { get; set; }
  public string                     Message      { get; set; }
  public string                     Body         { get; set; }
  public HttpEncoding               Encoding     { get; set; }

  public HttpMessage(string http) {
    Match match = ParseRegex.Match(http);

    RequestType = match.Groups["type"].Value.ToLower() switch {
      "get"    => HttpRequestType.Get,
      "post"   => HttpRequestType.Post,
      "delete" => HttpRequestType.Delete,
      "put"    => HttpRequestType.Put,
      "patch"  => HttpRequestType.Patch,
      ""       => HttpRequestType.None,
      _        => HttpRequestType.Other,
    };

    int.TryParse(
      match.Groups["code"].Value,
      NumberStyles.Integer,
      CultureInfo.InvariantCulture,
      out int code
    );

    Code       = code;
    Version    = match.Groups["version"].Value.Trim();
    Message    = match.Groups["message"].Value.Trim();
    RawHeaders = match.Groups["headers"].Value.Trim();
    Body       = match.Groups["body"].Value.Trim();
    Headers    = new Dictionary<string, string>();

    // May end with '0' fsr
    if (Body.EndsWith('0'))
      Body = Body.TrimEnd('0');
    // -----

    foreach (var header in RawHeaders.Split("\n")) {
      var headerMatch = HeaderRegex.Match(header);

      if (!headerMatch.Success) continue;

      var key   = headerMatch.Groups["key"].Value;
      var value = headerMatch.Groups["value"].Value;

      Headers.TryAdd(key, value);
    }

    ResponseType = Code switch {
      >= 100 and <= 199 => HttpResponseType.Progress,
      <= 299            => HttpResponseType.Ok,
      <= 399            => HttpResponseType.Redirect,
      <= 499            => HttpResponseType.ClientError,
      <= 599            => HttpResponseType.ServerError,
      _                 => HttpResponseType.Custom
    };

    if (
      Headers.TryGetValue("Content-Encoding", out string? encoding)
      && encoding.Contains("gzip", StringComparison.CurrentCultureIgnoreCase)
    ) {
      Encoding = HttpEncoding.Gzip;
    } else {
      Encoding = HttpEncoding.Identity;
    }
  }

  public override string ToString() {
    var sb = new StringBuilder();

    sb.AppendLine($"Type: {RequestType}");
    sb.AppendLine($"Version: {Version}");
    sb.AppendLine($"Code: {Code}");
    sb.AppendLine($"Message: {Message}");
    sb.AppendLine("Headers:");

    foreach (var header in Headers) {
      sb.AppendLine($"{header.Key}: {header.Value}");
    }

    sb.AppendLine("Body:");
    sb.AppendLine(Body);

    return sb.ToString();
  }

  [GeneratedRegex(
    @"^(?<type>\w+?\s)?HTTP/(?<version>[\d\.]+?)\s(?<code>\d+)\s(?<message>[\w\s]+?)\r?\n(?<headers>(?:.+\r?\n)*?)\r?\n(?<body>[\s\S]+)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant
  )]
  private static partial Regex GetParseRegex();

  [GeneratedRegex(@"(?<key>.+?):\s?(?<value>[^\r]+)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
  private static partial Regex GetHeaderRegex();
}
