using System.Text.Json.Nodes;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace XamantaSDK.OpenApi;

public sealed class ProtoSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Document is not { } document)
            return Task.CompletedTask;

        if (Annotation(context) is { } annotation)
        {
            if (Descriptor(annotation.Message) is { } stored)
                Reference(stored, document);
        }
        else if (Descriptor(context.JsonTypeInfo.Type) is { } message)
        {
            Describe(schema, message, document);
        }

        return Task.CompletedTask;
    }

    private static ProtoSchemaAttribute? Annotation(OpenApiSchemaTransformerContext context) =>
        context.JsonPropertyInfo?.AttributeProvider
            ?.GetCustomAttributes(typeof(ProtoSchemaAttribute), inherit: false)
            .OfType<ProtoSchemaAttribute>().FirstOrDefault();

    private static MessageDescriptor? Descriptor(Type type) =>
        typeof(IMessage).IsAssignableFrom(type)
            ? (MessageDescriptor?)type.GetProperty(nameof(IMessage.Descriptor))?.GetValue(null)
            : null;

    private static void Describe(OpenApiSchema schema, MessageDescriptor message, OpenApiDocument document)
    {
        schema.Type = JsonSchemaType.Object;
        schema.Title = message.FullName;
        schema.Description = OneofNote(message);
        schema.Properties = message.Fields.InFieldNumberOrder()
            .ToDictionary(field => field.JsonName, field => (IOpenApiSchema)Field(field, document));
    }

    private static string? OneofNote(MessageDescriptor message)
    {
        var groups = message.Oneofs
            .Where(o => !o.IsSynthetic)
            .Select(o => $"{o.Name}: {string.Join(" | ", o.Fields.Select(f => f.JsonName))}")
            .ToList();

        return groups.Count == 0 ? null : "At most one member of each group may be set — " + string.Join("; ", groups) + ".";
    }

    private static OpenApiSchema Field(FieldDescriptor field, OpenApiDocument document) => field switch
    {
        { IsMap: true } => new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = Value(field.MessageType.Fields[2], document)
        },
        { IsRepeated: true } => new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = Value(field, document)
        },
        _ => Value(field, document)
    };

    private static OpenApiSchema Value(FieldDescriptor field, OpenApiDocument document) => field.FieldType switch
    {
        FieldType.Message or FieldType.Group => Reference(field.MessageType, document),
        FieldType.Enum => Reference(field.EnumType, document),
        FieldType.Bool => new OpenApiSchema { Type = JsonSchemaType.Boolean },
        FieldType.String => new OpenApiSchema { Type = JsonSchemaType.String },
        FieldType.Bytes => new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" },
        FieldType.Double => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "double" },
        FieldType.Float => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "float" },
        FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32 => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        FieldType.UInt32 or FieldType.Fixed32 => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },
        _ => new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Integer,
            Format = "int64",
            Description = "Written as a string; a JSON number is also accepted."
        }
    };

    private static OpenApiSchema Reference(MessageDescriptor message, OpenApiDocument document)
    {
        if (!Registered(document, message.FullName))
        {
            var schema = new OpenApiSchema();
            document.AddComponent(message.FullName, schema);
            Describe(schema, message, document);
        }

        return Reference(message.FullName);
    }

    private static OpenApiSchema Reference(EnumDescriptor value, OpenApiDocument document)
    {
        if (!Registered(document, value.FullName))
        {
            document.AddComponent(value.FullName, new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Title = value.FullName,
                Enum = value.Values.Select(v => (JsonNode)v.Name).ToList()
            });
        }

        return Reference(value.FullName);
    }

    private static OpenApiSchema Reference(string id) => new()
    {
        Extensions = new Dictionary<string, IOpenApiExtension>
        {
            ["$ref"] = new JsonNodeExtension($"#/components/schemas/{id}")
        }
    };

    private static bool Registered(OpenApiDocument document, string id) =>
        document.Components?.Schemas?.ContainsKey(id) == true;
}
