# Bind endpoint: POST /connect/bind/{provider} (interactive cookie auth)

Status: done

## Context

Decided 2026-07-13, [ADR-019](../../../docs/adr-019-bind-as-interactive-flow.md):
binding a new provider is an authenticated browser action, not a login. The
current `GET /api/v1/auth/{provider}/login?token=<access JWT>` puts a raw
token in the URL (history, proxy logs) and contradicts ADR-018's invariant.

## Acceptance

- New `POST /connect/bind/{provider}` (github/google):
  - authenticates the `Interactive` scheme and re-checks `sid` liveness
    against `Sessions` (same backstop as `/connect/authorize`); dead cookie →
    existing challenge to SPA login;
  - on success, issues the OpenIddict client `Challenge` with `bindUserId`
    and the validated `redirectUrl` in `AuthenticationProperties`;
  - GET is rejected (405). CSRF safety comes from POST + `SameSite=Lax` —
    no anti-forgery token needed.
- `OAuthController`: delete the `?token=` parameter, its
  `ValidateTokenAndGetUserId` resolution, and the bind branch of the login
  entry points. Login entry points otherwise unchanged.
- Bind completion mints **no session and no one-time auth code**: when
  `bindUserId` is present, the callback runs the attach/merge pipeline and
  302s to the SPA settings page with a success or error query indicator.
- Frontend: bind buttons submit a programmatic form POST to
  `/connect/bind/{provider}`; the token-appending branch in the API layer is
  deleted; settings page reads the success/error indicator and refreshes the
  provider list.
- Integration tests:
  - bind with live cookie+session attaches the provider, no new session row;
  - bind with revoked session → challenge, nothing attached;
  - GET `/connect/bind/github` → 405;
  - POST without cookie → challenge;
  - merge-during-bind path still revokes source sessions (ADR-003 regression).

## References

- `backend/AuthService/Controllers/OAuthController.cs`
- `backend/AuthService/Controllers/AuthorizationController.cs` — liveness-check pattern to reuse
- `frontend/src/api/index.ts` — token-appending branch to delete

## Comments

- 2026-07-13: resolved. `POST /connect/bind/{provider}` with interactive
  cookie + DB liveness; `?token=` param and its JWT resolution deleted; bind
  completion mints no session/auth code and 302s with `?bound=`/`?error=`
  (error enum codes only). Frontend `startBind` form-POST; ProvidersPage
  consumes the result query. Six BindFlowTests cover challenge/liveness/405/
  redirect-gate/404; the provider round-trip halves follow the
  OAuthChallengeTests precedent (attach/merge covered at OAuthService level).
  Note: cookie challenge is an explicit `/login?returnUrl=` redirect —
  under `[ApiController]` a bare Challenge resolves to the bearer scheme.
  Suite green (217) + frontend typecheck clean.
