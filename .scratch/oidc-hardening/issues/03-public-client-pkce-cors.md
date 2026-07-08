# Public client (SPA) support: PKCE-required client type + CORS

Status: done

## Context

No pure-frontend consumer exists yet, but the capability must be complete
(decided 2026-07-08: no half-built SPA story). Confidential clients (OpenList)
exchange tokens server-to-server; a SPA does it from the browser and today
would fail twice: the seeder only creates confidential clients, and the app
has no CORS at all.

## Acceptance

- `OidcClientOptions` gains a client type; public clients are seeded with
  `ClientTypes.Public`, no secret, and the per-client PKCE requirement
  (`Requirements.Features.ProofKeyForCodeExchange`) so PKCE cannot be
  downgraded per client. Confidential clients unchanged.
- CORS enabled for `/connect/token`, `/connect/userinfo`,
  `/.well-known/openid-configuration`, `/.well-known/jwks.json` (SPA OIDC
  libraries fetch discovery + JWKS from the browser).
- Allowed origins derived dynamically from the origins of registered clients'
  redirect URIs (cached) — no separate origin config to keep in sync; the
  future admin UI (ADR-017) inherits this for free.
- Integration tests: public client completes the code flow with PKCE and is
  rejected without it; token endpoint answers preflight for a registered
  origin and refuses an unregistered one.

## References

- `backend/AuthService/Services/OidcClientSeeder.cs`
- `backend/AuthService/Program.cs`
- `docs/adr-017-admin-role-and-ui-managed-oidc-clients.md` — client-type choice moves into the UI later

## Comments

- 2026-07-08: resolved in commit 218e170, suite green.
