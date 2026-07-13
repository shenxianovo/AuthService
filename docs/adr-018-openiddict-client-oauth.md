# ADR-018: Replace Hand-Written OAuth Clients with OpenIddict Client

## Status: Accepted

Amended by [ADR-019](adr-019-bind-as-interactive-flow.md) (2026-07-13): the
bind-`userId`-from-`?token=` mechanism in decision 2 is superseded by the
interactive bind flow (`POST /connect/bind/{provider}`).

## Date: 2026-07-09

## Context

Upstream GitHub/Google login was implemented by hand: `OAuthController` built
authorize URLs string-by-string, `OAuthSecurityService` signed a custom state
payload with DataProtection, and `GithubAuthService`/`GoogleAuthService`
performed the code exchange and userinfo HTTP calls against hand-mapped DTOs
([ADR-005](adr-005-oauth-state-and-redirect.md)). It worked, but every line of
it re-implements what a maintained client stack does natively — CSRF
correlation, state protection, code exchange, claim mapping, provider quirks.

Two candidates were considered: the conventional ASP.NET remote authentication
handlers (`Microsoft.AspNetCore.Authentication.Google` + aspnet-contrib
GitHub), and `OpenIddict.Client` with its `WebIntegration` provider catalog.
The integration shape is identical either way (Challenge → protected state →
callback → domain pipeline). OpenIddict.Client was chosen for **a single
mental model**: this service already runs OpenIddict Server + Validation
([ADR-016](adr-016-openiddict-oidc-provider.md)) — one vendor, one options
style, one key-management story across both sides of the federation. It also
upgrades Google to a full OIDC code flow (PKCE + nonce + id_token validation)
and makes future providers one registration line.

Separately, the legacy `?redirect=` handoff (frontend appends **raw tokens** to
an allow-listed external URL) is fully superseded by the OIDC authorization
code flow: downstream services initiate login themselves via
`/connect/authorize` and receive a one-time code, never tokens. Its last
consumer (Heartbeat) has been taken offline.

## Decision

1. **Adopt `OpenIddict.Client` + `OpenIddict.Client.WebIntegration`**
   (`AddOpenIddict().AddClient(...)` alongside the existing server/validation).
   State tokens sign/encrypt with the same RSA key (`IRsaKeyProvider`) and
   `Oidc:EncryptionKey` as the server, bound via `IConfigureOptions` so tests
   can swap keys. Providers register only when credentials are configured, so
   credential-less local stacks still boot.
2. **Flow**: `GET /api/v1/auth/{provider}/login` validates `redirectUrl`,
   resolves an optional bind `userId` from `?token=`, and issues a `Challenge`
   carrying both in `AuthenticationProperties` — protected into the OAuth
   `state` by the framework, replacing the hand-rolled
   `GenerateState`/`ValidateState`. Redirect URIs **keep the previously
   registered callback paths** (`/api/v1/auth/{provider}/callback`), so the
   GitHub/Google console entries need no change. The callback actions read the
   validated principal via passthrough and run the unchanged domain pipeline:
   `ProcessOAuthLoginAsync` → session → one-time auth code → SPA.
   `email_verified` ([ADR-012](adr-012-oauth-email-verification-trust.md))
   comes from Google's OIDC claims directly and from a `/user/emails`
   backchannel call for GitHub.
3. **Delete the legacy `?redirect=` token handoff** (frontend
   `externalRedirect` store and branches). External services integrate via
   OIDC clients only. `AllowedRedirectOrigins` shrinks from
   `https://*.shenxianovo.com` to the SPA's own origin — the only remaining
   `redirectUrl` consumer is the SPA's own OAuth round-trip.

## Consequences

- ✅ ~300 lines of bespoke protocol code (state signing, token exchange, DTOs)
  replaced by the same library that already powers the server side; adding a
  provider becomes one registration line.
- ✅ Google upgrades to full OIDC (PKCE + nonce + id_token validation).
- ✅ Raw tokens no longer ever appear in URLs; the open-ended wildcard
  redirect surface is gone.
- ✅ GitHub/Google console registrations untouched (callback paths preserved).
- ⚠️ `OAuthSecurityService` keeps only redirect validation + one-time auth
  codes; its state-signing half (and ADR-005's state decision) is superseded.
- ⚠️ Less community documentation than the mainstream ASP.NET handlers —
  accepted in exchange for stack coherence.

## References

- [ADR-005](adr-005-oauth-state-and-redirect.md) — state-signing half superseded by this ADR
- [ADR-012](adr-012-oauth-email-verification-trust.md) — email trust rules the claim mapping preserves
- [ADR-016](adr-016-openiddict-oidc-provider.md) — the OpenIddict server this joins
- [`OAuthController.cs`](../backend/AuthService/Controllers/OAuthController.cs)
- [`OAuthService.cs`](../backend/AuthService/Services/OAuthService.cs) — unchanged domain pipeline
