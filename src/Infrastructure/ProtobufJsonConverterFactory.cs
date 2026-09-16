using Google.Protobuf;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XamantaSDK.Infrastructure;

public class ProtobufJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeof(IMessage).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(ProtobufJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public class ProtobufJsonConverter<T> : JsonConverter<T> where T : IMessage<T>, new()
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = JsonSerializer.Deserialize<JsonElement>(ref reader).GetRawText();
        return ProtoJson.Parser.Parse<T>(raw);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteRawValue(ProtoJson.Formatter.Format(value));
    }
}
