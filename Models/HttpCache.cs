namespace Go2Web.Models;

public class HttpCache {
  private readonly string _cacheDirectory;

  public HttpCache(string cacheDirectory) {
    _cacheDirectory = cacheDirectory;

    if (!Directory.Exists(cacheDirectory))
      Directory.CreateDirectory(cacheDirectory);
  }

  public void Add(Uri uri, string content) {
    string path = Uri.EscapeDataString(uri.GetLeftPart(UriPartial.Path));

    File.WriteAllText(
      Path.Combine(_cacheDirectory, path),
      content,
      Config.GlobalEncoding
    );
  }

  public string? Get(Uri uri) {
    string path = Path.Combine(_cacheDirectory,
      Uri.EscapeDataString(uri.GetLeftPart(UriPartial.Path)));

    return File.Exists(path)
      ? File.ReadAllText(
        path,
        Config.GlobalEncoding
      )
      : null;
  }

  public int Clear() {
    if (!Directory.Exists(_cacheDirectory)) return 0;

    int count = Directory.GetFiles(_cacheDirectory).Length;
    Directory.Delete(_cacheDirectory, true);
    Directory.CreateDirectory(_cacheDirectory);
    return count;
  }
}
