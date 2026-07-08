using AuthService.Data;
using AuthService.Entities;
using AuthService.Common;
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
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

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
           .UseOpenIddict()
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

// OIDC provider (OpenIddict) options + startup client seeding
builder.Services.Configure<OidcOptions>(builder.Configuration.GetSection(OidcOptions.Section));
builder.Services.AddHostedService<OidcClientSeeder>();

// Admin surface: DB-checked policy (Role never rides in a token, see ADR-017)
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.Section));
builder.Services.AddHostedService<AdminBootstrapper>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AdminRequirementHandler>();
builder.Services.AddAuthorization(options =>
    options.AddPolicy(AuthConstants.AdminPolicy, policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new AdminRequirement())));

// CORS only for the OIDC endpoints; origins derive from registered clients.
builder.Services.AddCors();
builder.Services.AddSingleton<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider, OidcCorsPolicyProvider>();

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
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IEmailManagementService, EmailManagementService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    // Interactive cookie: lets /connect/authorize recognize the browser session.
    // The SPA keeps using bearer tokens; this cookie only travels to /connect/*.
    .AddCookie(AuthConstants.InteractiveScheme, options =>
    {
        options.Cookie.Name = "authservice.sso";
        options.Cookie.HttpOnly = true;
        // Cross-site top-level GET from the OIDC client must carry the cookie → Lax, not Strict.
        options.Cookie.SameSite = SameSiteMode.Lax;
        // TLS terminates at nginx (forwarded proto); plain http stays usable in dev.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Path = "/connect";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = false;
        options.LoginPath = "/login";
        options.ReturnUrlParameter = "returnUrl";
    });
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

// OpenIddict: OIDC provider so third-party clients (e.g. OpenList) can log in
// via the standard authorization code flow.
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("connect/authorize")
               .SetTokenEndpointUris("connect/token")
               .SetUserInfoEndpointUris("connect/userinfo")
               // Keep the pre-OpenIddict JWKS path so downstream services that
               // verify our session JWTs keep working without reconfiguration.
               .SetJsonWebKeySetEndpointUris(".well-known/jwks.json");

        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();

        options.RegisterScopes(Scopes.Profile, Scopes.Email);

        // Access tokens stay plain RS256 JWTs (same key as JwtService) so
        // existing offline verification against the JWKS is unaffected.
        options.DisableAccessTokenEncryption();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               // TLS terminates at nginx; the app itself listens on plain HTTP.
               .DisableTransportSecurityRequirement();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// Issuer, signing key and encryption key are bound here (not in AddServer) so they
// resolve through DI/IConfiguration at host build time: tests can override config
// and swap IRsaKeyProvider, and the WebApplicationFactory config overrides apply.
builder.Services.AddOptions<OpenIddictServerOptions>()
    .Configure<IRsaKeyProvider, IConfiguration>((options, keyProvider, config) =>
    {
        options.Issuer = new Uri(config["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured."));

        options.SigningCredentials.Add(new SigningCredentials(
            new RsaSecurityKey(keyProvider.PrivateKey), SecurityAlgorithms.RsaSha256));

        // Authorization codes / refresh tokens are encrypted JWTs; the key must
        // survive restarts or all in-flight codes and refresh tokens die.
        var encryptionKey = config["Oidc:EncryptionKey"];
        if (string.IsNullOrEmpty(encryptionKey))
            throw new InvalidOperationException(
                "Oidc:EncryptionKey is not configured. Generate one with 'openssl rand -base64 32' and store it in user-secrets or the environment.");
        options.EncryptionCredentials.Add(new EncryptingCredentials(
            new SymmetricSecurityKey(Convert.FromBase64String(encryptionKey)),
            SecurityAlgorithms.Aes256KW, SecurityAlgorithms.Aes256CbcHmacSha512));
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

// CORS must run before UseAuthentication: OpenIddict serves /connect/token and
// /.well-known/* from inside the authentication middleware.
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

// Discovery (/.well-known/openid-configuration) and JWKS (/.well-known/jwks.json)
// are now served by OpenIddict with the same RSA signing key JwtService uses.

app.Run();
