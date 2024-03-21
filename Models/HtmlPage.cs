using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace TumWebLab5.Models;

public class HtmlPage {
  private static readonly IConfiguration   _config;
  private static readonly IBrowsingContext _context;

  private readonly string    _originalHtml;
  private readonly Uri       _pageUri;
  private          IDocument _document;

  static HtmlPage() {
    _config  = Configuration.Default.WithDefaultLoader();
    _context = BrowsingContext.New(_config);
  }

  public HtmlPage(string html, Uri pageUri) {
    _originalHtml = html;
    _pageUri      = pageUri;
  }

  public async Task Init() {
    _document = await _context.OpenAsync(req => req.Content(_originalHtml));
  }

  public string GetContent() {
    var doc = _document.Clone() as IDocument;

    if (doc?.Body == null) {
      throw new Exception("Error parsing HTML document");
    }

    foreach (IElement script in doc.QuerySelectorAll("script").ToArray())
      script.Remove();

    foreach (IElement style in doc.QuerySelectorAll("style").ToArray())
      style.Remove();

    IHtmlElement body = doc.Body;
    IHtmlCollection<IElement>
      all = body.QuerySelectorAll("h1, h2, h3, h4, h5, h6, span, p, img, a, button");
    var sb    = new StringBuilder(1024);

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
}
