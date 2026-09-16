namespace XamantaSDK.Auth;

public sealed class InvalidScopeException : Exception
{
    public InvalidScopeException(string message) : base(message) { }
}
