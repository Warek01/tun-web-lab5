using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Go2Web.Models;

public class Config {
  [JsonPropertyName("MaxRedirects")]
  public int MaxRedirects { get; set; }

  [JsonPropertyName("RequestTimeout")]
  public int RequestTimeout { get; set; }

  public static Encoding GlobalEncoding { get; set; } = Encoding.Default;

  public static Config Read(string fileName) {
    if (!File.Exists(fileName))
      throw new FileNotFoundException();

    string  raw    = File.ReadAllText(fileName);
    Config? config = JsonSerializer.Deserialize<Config>(raw);

    if (config == null)
      throw new Exception($"Error parsing config file {fileName}");

    return config;
  }
}
