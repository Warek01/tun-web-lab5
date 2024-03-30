namespace Go2Web.Models.Http;

public class Response {
  public Dictionary<string, string> Headers       { get; set; } = new();
  public Status                     Status        { get; set; }
  public int                        StatusCode    { get; set; }
  public string                     StatusMessage { get; set; } = null!;
  public string                     Version       { get; set; } = null!;
  public string                     Body          { get; set; } = null!;
  public ContentType                ContentType   { get; set; }
}
