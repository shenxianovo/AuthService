using AuthService.Data;
using AuthService.Entities;
using AuthService.Configuration;
using AuthService.Middleware;
using AuthService.Services;
using Resend;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using NSwag.Generation.Processors.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddDataProtection();
builder.Services.AddHealthChecks();

// OpenAPI / NSwag
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "AuthService API";
    config.Version = "v1";
    config.Description = "Authentication & Authorization service — password, OAuth (GitHub/Google), JWT RS256, session management.";

    // Add Bearer security scheme
    config.AddSecurity("Bearer", new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste your JWT access token here (without 'Bearer ' prefix).",
    });

    // Apply Bearer to all endpoints that require authorization
    config.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});

// nginx forwarded headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// JWT
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.AddSingleton<IRsaKeyProvider, PemFileRsaKeyProvider>();
builder.Services.AddSingleton<IJwtService, JwtService>();

// Github OAuth
builder.Services.Configure<GithubOAuthOptions>(builder.Configuration.GetSection(GithubOAuthOptions.Section));

// Google OAuth
builder.Services.Configure<GoogleOAuthOptions>(builder.Configuration.GetSection(GoogleOAuthOptions.Section));

// OAuth Security
builder.Services.Configure<OAuthSecurityOptions>(builder.Configuration.GetSection(OAuthSecurityOptions.Section));

// Password hasher (uses ASP.NET Core Identity's battle-tested implementation)
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// Auth services
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<IPasswordAuthService, PasswordAuthService>();
builder.Services.AddSingleton<IOAuthSecurityService, OAuthSecurityService>();
builder.Services.AddHttpClient<IGithubAuthService, GithubAuthService>();
builder.Services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();

// Resend Email
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o =>
{
    o.ApiToken = builder.Configuration["Resend:ApiKey"] ?? string.Empty;
});
builder.Services.AddTransient<IResend, ResendClient>();
builder.Services.AddScoped<IEmailService, ResendEmailService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IEmailManagementService, EmailManagementService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IJwtService, IConfiguration>((options, jwtService, config) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = jwtService.GetPublicKey(),
        };
    });

var app = builder.Build();

// Nginx forwarded headers
app.UseForwardedHeaders();

// Auto-apply migrations on startup (safe for single-instance deployment).
// Guarded on IsRelational so integration tests using the InMemory provider —
// which has no migrations — don't hit relational-only MigrateAsync.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

// NSwag: serve OpenAPI spec + Swagger UI (all environments; restrict in prod if needed)
app.UseOpenApi();      // /swagger/v1/swagger.json
app.UseSwaggerUi();   // /swagger

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

// Minimal OIDC discovery — allows JWT Bearer middleware to auto-discover keys via Authority
app.MapGet("/.well-known/openid-configuration", (IConfiguration config, HttpContext ctx) =>
{
    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    return Results.Json(new
    {
        issuer = config["Jwt:Issuer"],
        jwks_uri = $"{baseUrl}/.well-known/jwks.json",
    });
}).AllowAnonymous();

// JWKS endpoint — allows downstream services to verify JWTs offline
app.MapGet("/.well-known/jwks.json", (IJwtService jwtService) =>
{
    var key = jwtService.GetPublicKey();
    var parameters = key.Rsa!.ExportParameters(false);
    var jwk = new
    {
        keys = new[]
        {
            new
            {
                kty = "RSA",
                use = "sig",
                alg = "RS256",
                n = Base64UrlEncoder.Encode(parameters.Modulus!),
                e = Base64UrlEncoder.Encode(parameters.Exponent!),
            }
        }
    };
    return Results.Json(jwk);
}).AllowAnonymous();

app.Run();
