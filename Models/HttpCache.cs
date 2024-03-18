namespace TumWebLab5.Models;

public class HttpCache {
  public class Entry {
    public string   Url       { get; }
    public string   Value     { get; }
    
    // TODO: timeout mechanism
    // public DateTime CreatedAt { get; }
    // public TimeSpan TTL   { get; }
    // public bool     IsValid   { get; set; }

    public Entry(string url, string value) {
      Url   = url;
      Value = value;
    }
  }

  private readonly List<Entry> _entries = new();

  public HttpCache() { }

  public void Add(Entry entry) {
    _entries.Add(entry);
  }

  public string? Get(string url) {
    return _entries.Find(e => e.Url == url)?.Value;
  }
}
