# AuthService

A self-hosted central authentication service for `*.shenxianovo.com`.

Users sign in at `auth.shenxianovo.com`; the service issues RS256 JWT access tokens that any downstream service (e.g. `blog.shenxianovo.com`, `shenxianovo.com/heartbeat`, future projects) can verify with only the public key — no shared session store required.

**Not a permission/RBAC system.** This is a unified login & account management portal.

## Architecture

```
Client (Web / App)
  │
  │  register / login / OAuth
  ▼
┌──────────────────────────────────────┐
│  AuthService  (auth.shenxianovo.com) │
│                                      │
│  Session (PostgreSQL)  ← stateful    │
│  JWT Access Token      ← stateless   │
│  Refresh Token Rotation              │
│  RS256 asymmetric signing            │
│  API Key → JWT exchange              │
│                                      │
│  OAuth: GitHub, Google               │
│  Password: ASP.NET Identity Hasher   │
│  Email: Resend                       │
└──────────────────┬───────────────────┘
                   │ public key / API Key exchange
                   ▼
         ┌───────────────────┐
         │  Other Services   │
         │  verify JWT only  │
         │  (no DB lookup)   │
         └───────────────────┘
```

> See [ADR-001](docs/adr-001-session-plus-jwt.md) for why Session + JWT hybrid.

## Tech Stack

| Layer | Choice |
|-------|--------|
| Runtime | .NET 10 / ASP.NET Core |
| Database | PostgreSQL + EF Core (Code First) |
| Auth | JWT RS256, OAuth2 (GitHub, Google), password |
| Email | Resend |
| API Docs | NSwag (OpenAPI + Swagger UI) |
| Tests | xUnit v3 + Moq + InMemory DB + `WebApplicationFactory` |
| Deploy | systemd on single server, GitHub Actions CI/CD |

## Project Structure

```
backend/
├── AuthService/                          # Main service
│   ├── Controllers/                      # API endpoints
│   │   ├── PasswordAuthController.cs     #   register, login, add-password
│   │   ├── SessionController.cs          #   refresh, logout
│   │   ├── OAuthController.cs            #   OAuth login/callback (GitHub, Google)
│   │   ├── ExchangeController.cs         #   auth-code → token exchange
│   │   ├── ApiKeyController.cs           #   API key create/list/revoke/exchange
│   │   ├── UserController.cs             #   me, unlink-provider
│   │   └── HealthController.cs           #   health check
│   ├── Services/                         # Business logic
│   │   ├── PasswordAuthService.cs        #   register, login, add-password
│   │   ├── SessionService.cs             #   create/refresh/revoke sessions
│   │   ├── OAuthService.cs               #   OAuth login + user merge
│   │   ├── OAuthSecurityService.cs       #   state signing, auth-code cache
│   │   ├── JwtService.cs                 #   RS256 token generation/validation
│   │   ├── UserService.cs                #   user info, unlink provider
│   │   ├── GithubAuthService.cs          #   GitHub OAuth API client
│   │   ├── GoogleAuthService.cs          #   Google OAuth API client
│   │   ├── EmailVerificationService.cs   #   email verification codes
│   │   ├── ApiKeyService.cs              #   API key CRUD + exchange for JWT
│   │   ├── EmailManagementService.cs     #   add/remove/set-primary email
│   │   └── ResendEmailService.cs         #   Resend API wrapper
│   ├── Entities/                         # EF Core entities
│   ├── Data/                             # DbContext + migrations + configurations
│   ├── DTOs/                             # Request/response models
│   ├── Common/                           # Result<T> pattern, error codes
│   ├── Options/                          # Strongly-typed config (JWT, OAuth, etc.)
│   ├── Middleware/                        # Global exception handler
│   ├── Extensions/                       # Controller helper extensions
│   ├── Keys/                             # RSA key pair (private.pem, public.pem)
│   ├── Docs/                             # DB ER diagram (mermaid)
│   └── Program.cs                        # DI registration + pipeline
│
├── AuthService.Tests/                    # Test project
│   ├── Fixtures/
│   │   ├── DbTestBase.cs                #   shared InMemory DB setup for unit tests
│   │   └── ApiTestFixture.cs            #   WebApplicationFactory for integration tests
│   ├── Unit/Services/                    # Service-level unit tests
│   └── Integration/Controllers/          # HTTP-level integration tests
│
└── docs/                                 # Architecture Decision Records
    ├── adr-001-session-plus-jwt.md
    ├── adr-002-rs256-asymmetric-signing.md
    ├── adr-003-oauth-user-merge.md
    ├── adr-004-auth-code-exchange.md
    ├── adr-005-oauth-state-and-redirect.md
    ├── adr-006-soft-delete.md
    ├── adr-007-refresh-token-rotation.md
    ├── adr-008-result-pattern.md
    └── adr-template.md
```

## Database

EF Core Code First with PostgreSQL. See [ER diagram](backend/AuthService/Docs/Db.md).

Key tables: `Users`, `UserEmails`, `AuthProviders`, `PasswordCredentials`, `Sessions`, `RefreshTokens`, `EmailVerifications`, `PasswordResets`, `ApiKeys`.

## Design Decisions

| ADR | Date | Topic |
|-----|------|-------|
| [001](docs/adr-001-session-plus-jwt.md) | 2026-03-13 | Session + JWT hybrid architecture |
| [002](docs/adr-002-rs256-asymmetric-signing.md) | 2026-03-14 | RS256 asymmetric signing over HS256 |
| [003](docs/adr-003-oauth-user-merge.md) | 2026-03-20 | OAuth login user merge strategy |
| [004](docs/adr-004-auth-code-exchange.md) | 2026-03-20 | Auth code exchange instead of direct token return |
| [005](docs/adr-005-oauth-state-and-redirect.md) | 2026-03-20 | OAuth state signing + redirect whitelist |
| [006](docs/adr-006-soft-delete.md) | 2026-03-20 | Soft delete users instead of hard delete |
| [007](docs/adr-007-refresh-token-rotation.md) | 2026-04-13 | Refresh Token Rotation + hash storage |
| [008](docs/adr-008-result-pattern.md) | 2026-04-13 | Result pattern over exception-driven error handling |
| [009](docs/adr-009-api-key-exchange.md) | 2026-04-22 | API Key issuance + exchange for short-lived JWT |
