# Redirect validator cleanup: drop wildcard matching, error codes only

Status: done

## Context

Decided 2026-07-13 (grilling session): no compatibility with the `?redirect=`
era, no historical baggage. Two leftovers in the SPA OAuth round-trip:

- `OAuthSecurityService.ValidateRedirectUrl` still implements
  `https://*.example.com` wildcard subdomain matching
  (`OAuthSecurityService.cs:44-60`) — dead since ADR-018 shrank
  `AllowedRedirectOrigins` to the SPA's own origin. Keeping it means one
  config line silently reopens the subdomain redirect surface.
- `OAuthController.CompleteAsync` reflects the raw `result.ErrorMessage`
  into the `?error=` redirect query. Internal wording is not a contract;
  the SPA should map codes to copy itself.

## Acceptance

- Validator does exact origin match only; a configured origin containing
  `*` fails fast at startup.
- Error redirects carry `?error=<AuthError enum name>` only; SPA maps the
  code to user-facing text. No raw messages in URLs.
- Tests: wildcard config rejected at boot; error redirect carries the code,
  not the message.

## References

- `backend/AuthService/Services/OAuthSecurityService.cs`
- `backend/AuthService/Controllers/OAuthController.cs`
- `.scratch/oidc-backlog/issues/04-retire-redirect-handoff.md` — the removal
  this is the residue of

## Comments

- 2026-07-13: resolved. Validator is exact-origin only; wildcard config
  fails at startup via options ValidateOnStart; login error redirects carry
  the AuthError enum name only (bind branch already did). Unit tests
  inverted (subdomain/port now rejected) + startup validation test.
  Suite green (218).
