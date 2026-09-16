using System.Text.Json;
using Google.Protobuf;

namespace XamantaSDK.Infrastructure;

public static class ProtoJson
{
    public static readonly JsonParser Parser =
        new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    public static readonly JsonFormatter Formatter = JsonFormatter.Default;

    public static JsonElement? Raw(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
