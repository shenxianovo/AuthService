# ADR-016: OpenIddict OIDC Provider

## Status: Accepted

## Date: 2026-07-08

[`bd5b78d`](https://github.com/shenxianovo/AuthService/commit/bd5b78d) … [`24f0062`](https://github.com/shenxianovo/AuthService/commit/24f0062) — feat(oidc): OpenIddict provider series

## Context

AuthService could authenticate its own users (password, GitHub/Google upstream
OAuth) but could not act as an identity provider for third-party applications.
The concrete need: a self-hosted OpenList instance should log users in via
standard OIDC against this service. That requires the full provider surface —
discovery, authorization endpoint, token endpoint, userinfo, client
registration — which the hand-rolled `/.well-known` endpoints (issuer +
JWKS only) did not provide. The pre-existing `?redirect=` token-handoff
mechanism appends raw tokens to an unvalidated URL and is not a substitute.

## Decision

Adopt **OpenIddict 7.x** with EF Core stores on the existing `AppDbContext`
rather than hand-rolling the protocol or deploying a separate IdP (Keycloak).

- **Flows**: authorization code (+ refresh token grant). PKCE supported but not
  required — OpenList does not send it.
- **Signing key**: the same RSA pair `JwtService` uses, injected from
  `IRsaKeyProvider` via deferred options. OpenIddict serves the JWKS at the
  pre-existing `/.well-known/jwks.json` path, so downstream verification of
  session JWTs is unchanged (pinned by a key-equality regression test).
- **Encryption key**: authorization codes / refresh tokens are encrypted JWTs;
  a stable symmetric key comes from `Oidc:EncryptionKey` (fail-fast if absent).
- **Interactive cookie**: the SPA is bearer-only, but `/connect/authorize` is a
  top-level browser GET that must see the login state server-side. Login-
  completing endpoints (password register/login, one-time code exchange) also
  issue an `Interactive` cookie (HttpOnly, SameSite=Lax, `Path=/connect`, 30 d)
  carrying only `sub` + `sid`. The authorize endpoint re-validates the `sid`
  against the `Sessions` table on every request — the database, not the cookie,
  decides whether SSO is allowed; logout/revocation takes effect immediately.
- **Unauthenticated authorize** → cookie challenge → SPA
  `/login?returnUrl=<authorize URL>`; the SPA resumes the flow with a full-page
  navigation after login. Only same-origin `/connect/authorize` return URLs are
  honored.
- **Clients** live in the OpenIddict tables and are managed through the admin
  UI/API (superseded here by [ADR-017](adr-017-admin-role-and-ui-managed-oidc-clients.md);
  originally they were seeded from an `Oidc:Clients` config section). All
  clients are confidential-or-public with implicit consent — first-party
  self-hosted apps get no consent screen. Redirect URIs are exact-match
  including query strings (OpenList calls back with `?method=...` variants).
- **Claims**: the username is emitted as both `name` and `preferred_username`
  in the id_token so either OpenList "username key" setting works; email
  claims travel only in id_token/userinfo under the `email` scope, and **only
  when the primary email is verified** — registration doesn't require
  verification, and off-the-shelf RPs can't be trusted to check
  `email_verified` (mirror of the [ADR-012](adr-012-oauth-email-verification-trust.md)
  trust boundary). An unverified address is never asserted downstream.

## Consequences

- ✅ Any standard OIDC client can now use AuthService for SSO; OpenList is just
  the first seeded client.
- ✅ Existing surfaces are untouched: GitHub/Google upstream login, API-key
  exchange, session JWTs and their downstream verification all work as before.
- ✅ Session revocation propagates to **new authorize requests** instantly via
  the authorize-time DB check. Already-issued RP credentials live on the
  grant's own lifecycle instead: access tokens until expiry, refresh tokens
  until the grant is revoked — by design, logout does not end downstream
  logins, but soft-delete/merge must (decided 2026-07-09, see
  `.scratch/oidc-backlog/issues/05-grant-lifecycle-liveness.md`).
- ⚠️ Two secrets to manage per deployment: `Oidc:EncryptionKey` (must never
  rotate casually) and one client secret per registered app.
- ⚠️ OpenIddict-issued tokens carry the issuer with a trailing slash
  (`https://auth.shenxianovo.com/`); clients must be configured with the exact
  `issuer` string from the discovery document.
- ⚠️ The legacy `?redirect=` token handoff still exists; new integrations
  should use OIDC instead, and it can be removed once nothing depends on it.

## References

- [`Program.cs`](../backend/AuthService/Program.cs) — OpenIddict server/validation + cookie scheme wiring
- [`AuthorizationController.cs`](../backend/AuthService/Controllers/AuthorizationController.cs) — authorize endpoint, session backstop, claim destinations
- [`UserinfoController.cs`](../backend/AuthService/Controllers/UserinfoController.cs) — scope-gated userinfo
- [`OidcClientSeeder.cs`](../backend/AuthService/Services/OidcClientSeeder.cs) — idempotent client seeding
- [`InteractiveSignInExtensions.cs`](../backend/AuthService/Extensions/InteractiveSignInExtensions.cs) — cookie issuance on login
- [`oidcReturnUrl.ts`](../frontend/src/stores/oidcReturnUrl.ts) — SPA returnUrl consumption
- [`AuthorizeFlowTests.cs`](../backend/AuthService.Tests/Integration/Oidc/AuthorizeFlowTests.cs) — end-to-end code flow tests

## OpenList configuration (operator notes)

| OpenList setting | Value |
|---|---|
| SSO login platform | `OIDC` |
| SSO client ID | the client id you register in the admin UI (e.g. `openlist`) |
| SSO client secret | shown once when creating the client in the admin UI |
| SSO endpoint name | the exact `issuer` from `/.well-known/openid-configuration` (e.g. `https://auth.shenxianovo.com/`) |
| OIDC username key | `name` (or `preferred_username`) |
| SSO extra scopes | `profile email` |
| SSO JWT public key | leave empty (JWKS auto-discovery) |
| SSO compatibility mode | off |

The OpenList callback URL must be listed (with its `?method=...` variants) in
the client's redirect URIs in the admin UI.
