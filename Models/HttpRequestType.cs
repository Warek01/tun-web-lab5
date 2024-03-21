namespace Go2Web.Models;

public enum HttpRequestType {
  Get,
  Post,
  Patch,
  Put,
  Delete,
  Other,
  /// <summary>Is response</summary>
  None,
}
