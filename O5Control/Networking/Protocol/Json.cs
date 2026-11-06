using System.Text.Json;
using System.Text.Json.Serialization;

namespace O5Control.Networking.Protocol;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static T? Read<T>(JsonElement element) => element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
        ? default
        : element.Deserialize<T>(Options);
}
