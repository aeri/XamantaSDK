using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using XamantaSDK.Auth.Models;

namespace XamantaSDK.Auth;

public static class ClientMetadataValidator
{
    private const int MaxClientIdLength = 128;
    private const int MaxClientNameLength = 128;

    private static readonly string[] SupportedGrantTypes = ["client_credentials"];
    private static readonly string[] SupportedResponseTypes = [];
    private static readonly string[] SupportedAuthMethods = ["client_secret_basic", "client_secret_post"];

    public static bool TryNormalize(
        ClientRegistrationRequest request,
        string? enrollmentScopes,
        [NotNullWhen(true)] out ClientMetadata? metadata,
        [NotNullWhen(false)] out TokenError? error)
    {
        metadata = null;

        if (!string.IsNullOrWhiteSpace(request.SoftwareStatement))
            return Fail("invalid_software_statement", "software statements are not supported by this authorization server", out error);

        var hasJwks = request.Jwks is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined };
        var jwksUri = Trim(request.JwksUri);

        if (hasJwks && jwksUri is not null)
            return Fail("invalid_client_metadata", "jwks and jwks_uri must not both be present", out error);

        if (hasJwks || jwksUri is not null)
            return Fail("invalid_client_metadata", "this authorization server issues shared-secret clients only; jwks and jwks_uri are not supported", out error);

        if (request.RedirectUris is { Length: > 0 })
            return Fail("invalid_redirect_uri", "this authorization server issues client_credentials clients only; redirect_uris must not be registered", out error);

        if (request.GrantTypes is { } grantTypes &&
            !(grantTypes.Length > 0 && grantTypes.All(g => g == "client_credentials")))
            return Fail("invalid_client_metadata", "grant_types must hold client_credentials and nothing else", out error);

        if (request.ResponseTypes is { Length: > 0 })
            return Fail("invalid_client_metadata", "response_types must be empty for a client_credentials client", out error);

        var authMethod = Trim(request.TokenEndpointAuthMethod) ?? "client_secret_basic";
        if (!SupportedAuthMethods.Contains(authMethod, StringComparer.Ordinal))
            return Fail("invalid_client_metadata", $"token_endpoint_auth_method must be one of: {string.Join(", ", SupportedAuthMethods)}", out error);

        var androidId = Trim(request.AndroidId);
        if (androidId is null || androidId.Length > MaxClientIdLength)
            return Fail("invalid_client_metadata", $"android_id is required and must be at most {MaxClientIdLength} characters", out error);

        var clientName = Trim(request.ClientName);
        if (clientName is { Length: > MaxClientNameLength })
            return Fail("invalid_client_metadata", $"client_name must be at most {MaxClientNameLength} characters", out error);

        if (!Scopes.TryResolve(enrollmentScopes, request.Scope, out var scope, out var disallowed))
            return Fail("invalid_client_metadata", $"scope not allowed by this enrollment: {disallowed}", out error);

        var document = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["android_id"] = androidId,
            ["token_endpoint_auth_method"] = authMethod,
            ["grant_types"] = SupportedGrantTypes,
            ["response_types"] = SupportedResponseTypes
        };

        Add(document, "client_name", clientName);
        Add(document, "scope", scope);
        Add(document, "software_id", Trim(request.SoftwareId));
        Add(document, "software_version", Trim(request.SoftwareVersion));
        Add(document, "imei", Trim(request.Imei));
        Add(document, "meid", Trim(request.Meid));
        Add(document, "serial", Trim(request.Serial));
        Add(document, "esid", Trim(request.Esid));
        Add(document, "manufacturer", Trim(request.Manufacturer));
        Add(document, "brand", Trim(request.Brand));
        Add(document, "model", Trim(request.Model));

        metadata = new ClientMetadata(androidId, scope, document);
        error = null;
        return true;
    }

    private static bool Fail(string code, string description, out TokenError error)
    {
        error = new TokenError(code, description);
        return false;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Add(IDictionary<string, object> document, string name, string? value)
    {
        if (value is not null)
            document[name] = value;
    }
}
