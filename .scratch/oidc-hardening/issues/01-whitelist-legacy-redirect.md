# Whitelist origins for the legacy ?redirect= token handoff

Status: done

## Context

`frontend/src/stores/externalRedirect.ts` + `redirectToExternal()` in
LoginPage/OAuthCallbackPage append the access token, refresh token and userId
to **any** URL passed via `?redirect=` — no validation (`new URL(...)` and go).
Open redirect + token exfiltration: `auth.shenxianovo.com/login?redirect=https://evil.com`
sends a victim's tokens to the attacker after login.

The mechanism stays alive until Heartbeat (its only consumer,
`D:\Code\Personal\Heartbeat`) migrates to OIDC, so patch the hole now.

## Acceptance

- `externalRedirect` is only honored when its origin matches an allowlist
  (mirror the backend's `OAuthSecurity:AllowedRedirectOrigins` semantics,
  including the `*.` wildcard; hardcoding the same list in the frontend is
  acceptable — it changes rarely).
- Invalid redirect → ignored (normal dashboard login), not an error page.
- Existing Heartbeat login keeps working.

## References

- `frontend/src/stores/externalRedirect.ts`
- `backend/AuthService/Services/OAuthSecurityService.cs` — `ValidateRedirectUrl` semantics to mirror
- Removal of the whole mechanism is tracked in oidc-backlog/04.

## Comments

- 2026-07-08: resolved in commit 7b03a67, suite green.
