using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using XamantaSDK.Auth;
using XamantaSDK.Data;
using XamantaSDK.Data.Stores;
using XamantaSDK.Infrastructure;
using XamantaSDK.OpenApi;
using XamantaSDK.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000, o => o.Protocols = HttpProtocols.Http2);

    options.Limits.Http2.KeepAlivePingDelay = TimeSpan.FromSeconds(150);
    options.Limits.Http2.KeepAlivePingTimeout = TimeSpan.FromSeconds(30);
});

var connectionString = builder.Configuration.GetConnectionString("XamantaDb")
    ?? "Data Source=xamanta.db";
builder.Services.AddDbContext<XamantaDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddSingleton<SecretProtector>();
builder.Services.AddSingleton<DeviceConnectionRegistry>();
builder.Services.AddScoped<ClientStore>();
builder.Services.AddScoped<EnrollmentStore>();
builder.Services.AddScoped<IssuedTokenStore>();
builder.Services.AddScoped<AuthAttemptStore>();
builder.Services.AddScoped<PolicyStore>();
builder.Services.AddScoped<DeviceStore>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<PolicyDispatcher>();
builder.Services.AddScoped<DeviceEraser>();

var jwtKey = builder.Configuration["OAuth:JwtSigningKey"]
    ?? throw new InvalidOperationException("JWT signing key is not configured");

var adminApiKey = builder.Configuration["Admin:ApiKey"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, AdminApiKeyAuthenticationHandler>(
        AdminApiKeyDefaults.AuthenticationScheme, null)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "XamantaSDK",
            ValidAudience = "XamantaSDK",
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var clientId = ctx.Principal?.FindFirstValue("sub");
                var clients = ctx.HttpContext.RequestServices.GetRequiredService<ClientStore>();
                if (clientId is null || await clients.FindByIdAsync(clientId) is null)
                    ctx.Fail("Device is no longer registered");
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddHealthChecks();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new ProtobufJsonConverterFactory()));

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new ProtobufJsonConverterFactory()));

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer<XamantaDocumentTransformer>();
    options.AddSchemaTransformer<ProtoSchemaTransformer>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<XamantaDbContext>().Database.Migrate();
}

if (string.IsNullOrEmpty(adminApiKey))
    app.Logger.LogWarning(
        "Admin__ApiKey is not configured: the REST admin API (/devices, /policies, /enrollments) " +
        "will reject every request with 401. Set it to operate those endpoints.");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGrpcService<PolicyGrpcService>();
app.MapGrpcService<CommandGrpcService>();

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
    app.MapOpenApi();
}

app.MapGet("/", () => "XamantaSDK")
    .WithTags("Service")
    .WithSummary("Liveness probe.")
    .WithDescription("Answers with the server name as `text/plain` as soon as the process is serving. Takes no parameters and touches neither the database nor any device connection.");

app.Run();
