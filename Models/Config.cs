using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TumWebLab5.Models;

public class Config {
  [JsonPropertyName("MaxRedirects")]
  public int MaxRedirects { get; set; }

  public static Encoding GlobalEncoding { get; set; } = Encoding.Default;

  public static Config Read(string fileName) {
    if (!File.Exists(fileName))
      throw new FileNotFoundException();
    
    var raw = File.ReadAllText(fileName); 
    var config = JsonSerializer.Deserialize<Config>(raw);

    if (config == null)
      throw new Exception($"Error parsing config file {fileName}");
    
    return config;
  }
}
