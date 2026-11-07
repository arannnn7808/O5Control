using System.Text.Json.Serialization;

namespace O5Control.Core;

internal sealed class Credential
{
    [JsonPropertyName("serverId")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("instance")]
    public string Instance { get; set; } = string.Empty;

    [JsonPropertyName("secret")]
    public string Secret { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(ServerId) && !string.IsNullOrWhiteSpace(Secret);
}
