# Retire the ?redirect= token handoff after Heartbeat migrates to OIDC

Status: needs-triage

## Context

Heartbeat (`D:\Code\Personal\Heartbeat`, separate repo — frontend login
button) is the only consumer of the legacy `?redirect=` handoff, which places
access + refresh tokens in URL query params. Cross-repo migration is
deliberately deferred until the AuthService-side work lands (decided
2026-07-08). The interim whitelist patch is oidc-hardening/01.

## AuthService-side removal (this issue)

Once Heartbeat logs in via OIDC (register it as a client — confidential if it
gets a backend, public+PKCE if it stays a SPA, see oidc-hardening/03):

- Delete `frontend/src/stores/externalRedirect.ts`, `redirectToExternal()` in
  LoginPage/OAuthCallbackPage, and the `?redirect=` init in App.vue.
- ADR-004/ADR-005 remain valid (they cover the SPA's own OAuth code exchange,
  not this mechanism) — verify before touching.
