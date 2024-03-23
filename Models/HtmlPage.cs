using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Go2Web.Models;

public class HtmlPage {
  private static readonly IBrowsingContext Context;

  private readonly string       _originalHtml;
  private readonly Uri          _pageUri;
  private          IDocument    _document = null!;
  private          IHtmlElement _body     = null!;

  static HtmlPage() {
    IConfiguration config = Configuration.Default.WithDefaultLoader();
    Context = BrowsingContext.New(config);
  }

  public HtmlPage(string html, Uri pageUri) {
    _originalHtml = html;
    _pageUri      = pageUri;
  }

  public async Task Init() {
    _document = await Context.OpenAsync(req => req.Content(_originalHtml));
    _body     = _document.Body!;

    ClearScripts();
    ClearStyles();
  }

  public string GetTextContent() {
    IHtmlCollection<IElement>
      all = _body.QuerySelectorAll("h1, h2, h3, h4, h5, h6, span, p, img, a, button");
    var sb = new StringBuilder(1024);

    foreach (IElement e in all)
      switch (e.NodeName.ToLower()) {
        case "img":
          string? src = e.GetAttribute("src");

          if (src == null) break;
          if (src.StartsWith('/')) src = new Uri(_pageUri, src).ToString();

          sb.AppendLine($"Image -> {src}");
          break;
        case "a":
          string? link = e.GetAttribute("href");

          if (link == null) break;
          if (link.StartsWith('/')) link = new Uri(_pageUri, link).ToString();

          sb.AppendLine($"Link -> {link}");
          break;
        default: {
          string text = e.TextContent.Trim();
          if (text.Length > 0) sb.AppendLine(text);
          break;
        }
      }

    return sb.ToString();
  }

  public string GetSearchResults() {
    var sb = new StringBuilder(1024);

    var googleLinkRegex = new Regex(@"^/url\?q=\w+?\.google.com");

    IHtmlCollection<IElement> allLinks    = _body.QuerySelectorAll("div:has(a)");
    var                       searchLinks = new Dictionary<string, string>();

    foreach (IElement e in allLinks) {
      IElement a    = e.QuerySelector("a")!;
      string   link = a.Attributes["href"]!.Value;
      string   text = a.TextContent;
      
      if (!link.StartsWith("/url?q=") || googleLinkRegex.Match(link).Success)
        continue;

      searchLinks.TryAdd(text, link["/url?q=".Length..]);

      if (searchLinks.Count == 10) break;
    }

    foreach (var pair in searchLinks) 
      sb.AppendLine($"{pair.Key}: {pair.Value}");

    return sb.ToString();
  }

  private void ClearStyles() {
    foreach (IElement script in _document.QuerySelectorAll("script").ToArray())
      script.Remove();
  }

  private void ClearScripts() {
    foreach (IElement style in _document.QuerySelectorAll("style").ToArray())
      style.Remove();
  }
}
