using System.Text;
using System.Text.RegularExpressions;

namespace TumWebLab5.Models;

public class HttpMessage {
  private static readonly Regex _parseRegex = new(
    @"^(?<type>\w+?\s)?HTTP/(?<version>[\d\.]+?)\s(?<code>\d+)\s(?<message>[\w\s]+?)\r?\n(?<headers>(?:.+\r?\n)*?)\r?\n(?<body>[\s\S]+)",
    RegexOptions.IgnoreCase
  );

  private static readonly Regex _headerRegex = new(
    @"(?<key>.+?):\s?(?<value>[^\r]+)",
    RegexOptions.IgnoreCase
  );

  public readonly HttpRequestType            Type;
  public readonly string                     RawHeaders;
  public readonly Dictionary<string, string> Headers;
  public readonly string                     Version;
  public readonly int                        Code;
  public readonly string                     Message;
  public readonly string                     Body;

  public HttpMessage(string http) {
    var match = _parseRegex.Match(http);

    HttpRequestType type = match.Groups["type"].Value.ToLower() switch {
      "get"    => HttpRequestType.Get,
      "post"   => HttpRequestType.Post,
      "delete" => HttpRequestType.Delete,
      "put"    => HttpRequestType.Put,
      "patch"  => HttpRequestType.Patch,
      _        => HttpRequestType.None,
    };

    Code       = int.Parse(match.Groups["code"].Value);
    Version    = match.Groups["version"].Value;
    Message    = match.Groups["message"].Value;
    RawHeaders = match.Groups["headers"].Value;
    Body       = match.Groups["body"].Value;
    Headers    = new Dictionary<string, string>();

    foreach (string header in RawHeaders.Split("\n")) {
      var headerMatch = _headerRegex.Match(header);

      if (!headerMatch.Success) continue;
      
      var key         = headerMatch.Groups["key"].Value;
      var value       = headerMatch.Groups["value"].Value;

      Headers.Add(key, value);
    }
  }

  public override string ToString() {
    var sb = new StringBuilder();

    sb.AppendLine($"Type: {Type}");
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
}
