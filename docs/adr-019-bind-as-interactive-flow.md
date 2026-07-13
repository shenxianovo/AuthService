# ADR-019: Bind as Interactive Flow

## Status: Accepted

## Date: 2026-07-13

## Context

Attaching a new OAuth provider to an already-logged-in account (the *binding
flow*) was implemented as a variant of login: the SPA navigated to
`GET /api/v1/auth/{provider}/login?token=<access JWT>`, and the endpoint
resolved the binding user from the token query parameter. That put a raw
access token in the URL — browser history, reverse-proxy access logs —
directly contradicting the invariant [ADR-018](adr-018-openiddict-client-oauth.md)
had just established ("raw tokens never appear in URLs").

The root cause is a category error: binding is not a login, it is an
**authenticated browser action**. Treating it as a login forces the identity
proof through the URL, because the SPA is bearer-only and a top-level
navigation carries no Authorization header. Meanwhile the service already
owns the right primitive: the interactive cookie
([ADR-016](adr-016-openiddict-oidc-provider.md)) identifies the logged-in
browser to `/connect/authorize`, with a DB session-liveness check on every
request. This is exactly how GitHub/Google implement their own "connected
accounts" pages — a session-cookie-authenticated POST that starts the OAuth
dance server-side.

Alternatives considered: a one-time bind ticket minted via authenticated POST
(keeps bearer purity, but adds a second one-time-code mechanism and keeps
bind masquerading as login); accepting the status quo (self-hosted, logs are
operator-only — but the fix is cheaper than the exception it would carve out).

## Decision

1. **Binding initiates at `POST /connect/bind/{provider}`**, a top-level form
   POST from the SPA. The endpoint authenticates the `Interactive` scheme and
   re-checks `sid` liveness against the `Sessions` table (same backstop as
   authorize), then issues the OpenIddict client `Challenge` with the
   resolved `bindUserId` in the protected state. The `?token=` query
   parameter and its JWT resolution in `OAuthController` are deleted.
2. **POST-only, relying on `SameSite=Lax`.** Cross-site top-level GETs send
   Lax cookies; cross-site POSTs do not. GET would enable forced binding
   (login-CSRF variant: an attacker binds *their* provider account to the
   victim's account, then logs in as the victim via that provider). POST +
   Lax closes this without anti-forgery tokens. GET requests are rejected.
3. **Bind completion mints no session.** The user is already logged in; the
   callback attaches/merges the provider and 302s back to the SPA settings
   page with a success or error indicator — no one-time auth code, no new
   session row.
4. **Expired-cookie edge stays manual.** If the cookie is dead but the SPA
   session alive, the endpoint challenges to the SPA login page; after
   re-login the user re-clicks bind. Resuming a POST via returnUrl is not
   worth the machinery.
5. The interactive cookie's scope statement widens from "identifies the
   browser to `/connect/authorize`" to "identifies the browser to the
   interactive endpoints under `/connect`". Its `Path=/connect` scoping
   already covers the new endpoint — no cookie change.

## Consequences

- ✅ ADR-018's invariant is restored globally: no raw token ever appears in a
  URL, with no exceptions to document.
- ✅ Net code deletion: `?token=` parsing, `ValidateTokenAndGetUserId` usage
  in `OAuthController`, and the frontend token-appending branch all go; no
  new ticket mechanism replaces them.
- ✅ One rule for browser-facing surfaces: everything interactive lives under
  `/connect` and is authenticated by the interactive cookie + DB check.
- ✅ Forced-binding CSRF is structurally closed (POST + Lax).
- ⚠️ The SPA must initiate bind with a programmatic form POST instead of a
  location change.
- ⚠️ Supersedes the bind half of ADR-018's flow description (its
  `?token=`-based bind resolution).

## References

- [ADR-016](adr-016-openiddict-oidc-provider.md) — interactive cookie + DB backstop this reuses
- [ADR-018](adr-018-openiddict-client-oauth.md) — bind-via-`?token=` superseded by this ADR
- [ADR-003](adr-003-oauth-user-merge.md) — merge semantics, unchanged
- [`OAuthController.cs`](../backend/AuthService/Controllers/OAuthController.cs)
- `.scratch/bind-interactive-flow/issues/01-bind-endpoint.md` — implementation issue
