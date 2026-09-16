namespace XamantaSDK.OpenApi;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ProtoSchemaAttribute(Type message) : Attribute
{
    public Type Message { get; } = message;
}
