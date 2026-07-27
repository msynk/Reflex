using System.Text.Json;

namespace Blex;

/// <summary>Shared <see cref="JsonSerializerOptions"/> used by generated serialization code.</summary>
public static class BlexJson
{
    /// <summary>Default options: camelCase-insensitive, enums as strings, tolerant reading.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
