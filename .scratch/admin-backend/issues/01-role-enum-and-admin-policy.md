# Role enum + RequireAdmin policy + bootstrap admin

Status: ready-for-agent

## Context

First slice of ADR-017. Registration is public, so admin cannot be
first-user-auto; and Role must be structurally invisible to downstream
services (no claim in any token).

## Acceptance

- `User.Role` enum (`User` / `Admin`) + migration; default `User`.
- `RequireAdmin` authorization policy whose handler reads the user's Role from
  the database per request (instant grant/revoke; **no role claim** in session
  JWTs, OIDC tokens, or userinfo — see the Role entry in CONTEXT.md).
- Bootstrap: config-designated admin (e.g. `Admin:BootstrapUsername`),
  promoted idempotently at startup; log a warning if the user doesn't exist.
- Tests: non-admin gets 403 on an admin endpoint; promotion/demotion takes
  effect without re-login; no `role` claim appears in any issued token.

## References

- `docs/adr-017-admin-role-and-ui-managed-oidc-clients.md`
- `CONTEXT.md` — Role glossary entry (authoritative semantics)
