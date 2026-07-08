# AuthService

A self-hosted central authentication service for `*.shenxianovo.com`.

Users sign in at `auth.shenxianovo.com`; the service issues RS256 JWT access tokens that any downstream service (e.g. `blog.shenxianovo.com`, `shenxianovo.com/heartbeat`, future projects) can verify with only the public key — no shared session store required. It is also a standard **OIDC provider** (OpenIddict): third-party apps such as OpenList log users in via the authorization code flow against `/connect/*`.

**Not a permission/RBAC system.** This is a unified login & account management portal — downstream services own their own permission models, keyed on the token's `sub`.

## Architecture

```
Client (Web / App)                 OIDC clients (OpenList, ...)
  │                                  │
  │  register / login / OAuth        │  authorization code flow
  ▼                                  ▼
┌──────────────────────────────────────┐
│  AuthService  (auth.shenxianovo.com) │
│                                      │
│  Session (PostgreSQL)  ← stateful    │
│  JWT Access Token      ← stateless   │
│  Refresh Token Rotation              │
│  RS256 asymmetric signing            │
│  API Key → JWT exchange              │
│  OIDC provider (OpenIddict)          │
│                                      │
│  OAuth: GitHub, Google               │
│  Password: ASP.NET Identity Hasher   │
│  Email: Resend                       │
└──────────────────┬───────────────────┘
                   │ public key (JWKS) / API Key exchange / OIDC tokens
                   ▼
         ┌───────────────────┐
         │  Other Services   │
         │  verify JWT only  │
         │  (no DB lookup)   │
         └───────────────────┘
```

> See [ADR-001](docs/adr-001-session-plus-jwt.md) for why Session + JWT hybrid, [ADR-016](docs/adr-016-openiddict-oidc-provider.md) for the OIDC provider.

## Tech Stack

| Layer | Choice |
|-------|--------|
| Runtime | .NET 10 / ASP.NET Core |
| Database | PostgreSQL + EF Core (Code First) |
| Auth | JWT RS256, OAuth2 (GitHub, Google), password, OIDC provider (OpenIddict) |
| Email | Resend |
| API Docs | NSwag (OpenAPI + Swagger UI) |
| Tests | xUnit v3 + Moq + InMemory DB + Testcontainers + `WebApplicationFactory` |
| Deploy | Docker Compose on a single server behind nginx (TLS), GitHub Actions CI/CD |

## Project Structure

```
backend/
├── AuthService/            # main service
│   ├── Controllers/        #   /api/v1/* endpoints + OIDC /connect/* endpoints
│   ├── Services/           #   business logic (sessions, OAuth, OIDC, JWT, email)
│   ├── Entities/           #   EF Core entities
│   ├── Data/               #   DbContext, migrations, configurations
│   ├── DTOs/ Common/ Options/ Middleware/ Extensions/
│   └── Program.cs          #   DI registration + pipeline
└── AuthService.Tests/      # unit + integration tests (fixtures in Fixtures/)

frontend/                   # Vue 3 SPA: login, dashboard, account management
docs/                       # Architecture Decision Records (adr-XXX-*.md)
CONTEXT.md                  # domain glossary — canonical terms for code and docs
```

## Local development

The day-to-day inner loop needs **no local configuration** — tests carry their own environment:

```powershell
cd backend; dotnet test           # InMemory API tests + Testcontainers Postgres tests (needs Docker)
cd frontend; npm run dev -- --mode mock   # SPA against the mock API plugin
```

For a full-stack end-to-end run (the real images, built locally):

1. One-time: generate a dev RSA key pair into `./secrets/` (gitignored):

   ```powershell
   # PowerShell (no openssl needed)
   New-Item -ItemType Directory -Force secrets | Out-Null
   $rsa = [System.Security.Cryptography.RSA]::Create(2048)
   Set-Content secrets/jwt_private.pem $rsa.ExportRSAPrivateKeyPem()
   Set-Content secrets/jwt_public.pem  $rsa.ExportSubjectPublicKeyInfoPem()
   ```

   ```bash
   # or openssl
   mkdir -p secrets
   openssl genrsa -out secrets/jwt_private.pem 2048
   openssl rsa -in secrets/jwt_private.pem -pubout -out secrets/jwt_public.pem
   ```

2. Build and start the stack (db + backend + frontend, dev credentials baked into the override file):

   ```powershell
   docker compose -f compose.yml -f compose.local.yml up --build -d
   ```

3. Open <http://localhost:8080> — register, browse the dashboard. Smoke the OIDC provider:

   ```powershell
   curl http://localhost:8080/.well-known/openid-configuration
   # then drive /connect/authorize → /connect/token as documented in ADR-016
   ```

4. Tear down with `docker compose -f compose.yml -f compose.local.yml down -v`.

## Frontend API Client

The TypeScript API client is auto-generated from the OpenAPI spec using [NSwag](https://github.com/RicoSuter/NSwag):

```bash
nswag openapi2tsclient \
  /input:http://localhost:5252/swagger/v1/swagger.json \
  /output:frontend/src/api/client.ts \
  /TypeScriptVersion:5.0 \
  /GenerateClientInterfaces:true \
  /Template:Fetch
```

Re-run this command whenever backend API endpoints change.

## Database

EF Core Code First with PostgreSQL. See [ER diagram](backend/AuthService/Docs/Db.md).

Key tables: `Users`, `UserEmails`, `AuthProviders`, `PasswordCredentials`, `Sessions`, `RefreshTokens`, `EmailVerifications`, `PasswordResets`, `ApiKeys`, plus the OpenIddict client/token tables.

## Design Decisions

Architecture Decision Records live in [`docs/`](docs/) (`adr-XXX-*.md`), and the domain glossary in [`CONTEXT.md`](CONTEXT.md) — start there before renaming or re-modeling anything.
