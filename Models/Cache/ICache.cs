namespace Go2Web.Models.Cache;

public interface ICache {
  public void    Add(Uri uri, string content);
  public string? Get(Uri uri);
  public int     Clear();
  public void    DeleteEntry();
}
