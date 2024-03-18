using System.Text;
using System.Text.RegularExpressions;

namespace TumWebLab5.Models;

public partial class HttpMessage {
  private static readonly Regex ParseRegex  = GetParseRegex();
  private static readonly Regex HeaderRegex = GetHeaderRegex();

  public readonly HttpRequestType            RequestType;
  public readonly HttpResponseType           ResponseType;
  public readonly string                     RawHeaders;
  public readonly Dictionary<string, string> Headers;
  public readonly string                     Version;
  public readonly int                        Code;
  public readonly string                     Message;
  public readonly string                     Body;

  public HttpMessage(string http) {
    var match = ParseRegex.Match(http);

    RequestType = match.Groups["type"].Value.ToLower() switch {
      "get"    => HttpRequestType.Get,
      "post"   => HttpRequestType.Post,
      "delete" => HttpRequestType.Delete,
      "put"    => HttpRequestType.Put,
      "patch"  => HttpRequestType.Patch,
      ""       => HttpRequestType.None,
      _        => HttpRequestType.Other,
    };

    Code       = int.Parse(match.Groups["code"].Value);
    Version    = match.Groups["version"].Value;
    Message    = match.Groups["message"].Value;
    RawHeaders = match.Groups["headers"].Value;
    Body       = match.Groups["body"].Value;
    Headers    = new Dictionary<string, string>();

    foreach (var header in RawHeaders.Split("\n")) {
      var headerMatch = HeaderRegex.Match(header);

      if (!headerMatch.Success) continue;

      var key   = headerMatch.Groups["key"].Value;
      var value = headerMatch.Groups["value"].Value;

      Headers.Add(key, value);
    }

    ResponseType = Code switch {
      >= 100 and <= 199 => HttpResponseType.Progress,
      <= 299            => HttpResponseType.Ok,
      <= 399            => HttpResponseType.Redirect,
      <= 499            => HttpResponseType.ClientError,
      <= 599            => HttpResponseType.ServerError,
      _                 => HttpResponseType.Custom
    };
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
