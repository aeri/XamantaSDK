using System.Text;

namespace XamantaSDK.Infrastructure;

public static class AuthorizationHeader
{
    public const string Realm = "XamantaSDK";

    private const string Basic = "Basic ";
    private const string Bearer = "Bearer ";

    public static string? BearerToken(this HttpRequest request)
    {
        if (!request.HasScheme(Bearer))
            return null;

        var token = request.Header()![Bearer.Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    public static (string Id, string Secret)? BasicCredentials(this HttpRequest request)
    {
        if (!request.HasScheme(Basic))
            return null;

        var encoded = request.Header()![Basic.Length..].Trim();
        if (string.IsNullOrEmpty(encoded))
            return null;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var colon = decoded.IndexOf(':');
            if (colon < 0)
                return null;

            return (Uri.UnescapeDataString(decoded[..colon]),
                    Uri.UnescapeDataString(decoded[(colon + 1)..]));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? Header(this HttpRequest request) =>
        request.Headers.Authorization.FirstOrDefault();

    private static bool HasScheme(this HttpRequest request, string scheme) =>
        request.Header()?.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) == true;
}
