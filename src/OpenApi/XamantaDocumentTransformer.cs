using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using XamantaSDK.Auth;

namespace XamantaSDK.OpenApi;

public sealed class XamantaDocumentTransformer : IOpenApiDocumentTransformer
{
    public const string EnrollmentToken = "enrollmentToken";
    public const string ClientSecretBasic = "clientSecretBasic";
    public const string DeviceAccessToken = "deviceAccessToken";
    public const string AdminApiKey = "adminApiKey";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "XamantaSDK",
            Version = "v1",
            Description = """
                Command-and-control server for the Xamanta Android Device Policy Controller (DPC).

                Three surfaces share the process, of which this document describes the two HTTP ones:

                - **OAuth** (`/connect`) — RFC 7591 dynamic registration and the RFC 6749
                  `client_credentials` token endpoint. Spoken by devices during provisioning; the
                  access tokens it issues are what the gRPC services accept.
                - **Admin REST** (`/enrollments`, `/policies`, `/devices`) — mints enrollment tokens,
                  publishes policy versions, assigns them and issues one-shot device commands.
                  Guarded by the `X-Admin-Api-Key` header (the `adminApiKey` scheme); keep it on a
                  trusted network / behind TLS all the same.
                - **gRPC** (`policy.PolicySyncService`, `commands.CommandService`) — device-facing
                  only, and not described here. Its contract is `Protos/policy.proto` and
                  `Protos/commands.proto`, and server reflection is enabled in Development.

                Bodies that carry a protobuf message use the proto3 JSON mapping: camelCase members,
                enums by symbolic name, 64-bit integers as strings, unknown members ignored. Their
                schemas are generated from the `.proto` descriptors and named after the proto type
                (`policy.DevicePolicy`, `commands.CommandType`, …). A field documented as holding a
                message but typed as free-form JSON is stored exactly as the device sent it; look up
                the named schema for its shape.
                """
        };

        document.Servers =
        [
            new OpenApiServer
            {
                Url = "http://localhost:5000",
                Description = "Development. Kestrel serves HTTP/2 cleartext (h2c) only, so REST clients need prior-knowledge HTTP/2. Production must terminate TLS."
            }
        ];

        document.Tags = new HashSet<OpenApiTag>
        {
            new() { Name = "OAuth", Description = "Device provisioning: dynamic client registration and token issuance." },
            new() { Name = "Enrollments", Description = "Admin: mint, list and revoke the enrollment tokens devices register with." },
            new() { Name = "Policies", Description = "Admin: publish and inspect named, versioned policies." },
            new() { Name = "Devices", Description = "Admin: device status, compliance history, policy assignment and commands." },
            new() { Name = "Service", Description = "Liveness." }
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [EnrollmentToken] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "One-time enrollment token minted by `POST /enrollments`, presented as the RFC 7591 §3.1 initial access token. Single use per activation, and it may expire."
            },
            [ClientSecretBasic] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "basic",
                Description = "`client_secret_basic` (RFC 6749 §2.3.1): base64 of `urlencode(client_id):urlencode(client_secret)`. The alternative is `client_secret_post` — the same credentials in the form body — but never both in one request."
            },
            [DeviceAccessToken] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Description = "Access token issued by the token endpoint. Not accepted by any operation in this document — it authenticates the device on the gRPC services.",
                Flows = new OpenApiOAuthFlows
                {
                    ClientCredentials = new OpenApiOAuthFlow
                    {
                        TokenUrl = new Uri("/connect/token", UriKind.Relative),
                        Scopes = new Dictionary<string, string>()
                    }
                }
            },
            [AdminApiKey] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = AdminApiKeyDefaults.HeaderName,
                Description = "Operator API key for the admin REST surface, presented in the `X-Admin-Api-Key` header. Configured server-side as `Admin__ApiKey`; every admin request is rejected with 401 until it is set."
            }
        };

        Secure(document, "/connect/register", HttpMethod.Post, Requirement(document, EnrollmentToken));

        Secure(document, "/connect/token", HttpMethod.Post, Requirement(document, ClientSecretBasic), []);

        var adminRequirement = Requirement(document, AdminApiKey);
        SecureAll(document, "/enrollments", adminRequirement);
        SecureAll(document, "/policies", adminRequirement);
        SecureAll(document, "/devices", adminRequirement);

        return Task.CompletedTask;
    }

    private static OpenApiSecurityRequirement Requirement(OpenApiDocument document, string scheme) =>
        new() { [new OpenApiSecuritySchemeReference(scheme, document)] = [] };

    private static void Secure(OpenApiDocument document, string path, HttpMethod method, params OpenApiSecurityRequirement[] requirements)
    {
        if (document.Paths.TryGetValue(path, out var item) && item.Operations?.TryGetValue(method, out var operation) == true)
            operation.Security = requirements;
    }

    private static void SecureAll(OpenApiDocument document, string prefix, OpenApiSecurityRequirement requirement)
    {
        foreach (var (path, item) in document.Paths)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal) || item.Operations is null)
                continue;

            foreach (var operation in item.Operations.Values)
                operation.Security = [requirement];
        }
    }
}
