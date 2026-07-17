# Username rename endpoint — GitHub model (immediate release)

Status: open

## Context

Decided 2026-07-17 (grilling session with Heartbeat, see Heartbeat ADR-027
and `.scratch/multi-user/issues/01-username-rename-landmine.md` there):
users can rename themselves; the old name is **released immediately** for
anyone to claim (GitHub model — no freeze period, no tombstone, no rate
limit; permanent reservation rejected as a state machine for nonexistent
namespace pressure).

Downstream (Heartbeat) defends itself via provisioning eviction, keyed on
the fact that `~` is outside our username charset — do not widen
`UsernameValidator`'s charset without checking downstream assumptions.

## Acceptance

- `PATCH /api/v1/users/me/username` on `UserController`:
  - validation chain identical to registration: `ToLowerInvariant()` →
    `UsernameValidator.IsValid` (incl. reserved list) →
    `IgnoreQueryFilters()` global uniqueness (incl. soft-deleted users).
    Extract the shared method from `PasswordAuthService.cs:31-50` instead of
    duplicating it.
  - no-op if the new name equals the current one.
- On success: `RevokeAllSessionsAsync(userId)` — **all** sessions including
  the caller's (old tokens carry the stale `preferred_username`; forced
  re-login is the accepted UX). Same unit of work as the rename.
- Accepted stale window (documented, no code): already-issued stateless
  tokens live to exp — session JWT 15 min, OIDC access token 1 h (OpenIddict
  default; alignment deferred to oidc-backlog issue 07).
- Frontend: rename entry on `ProfilePage.vue` (pattern: SecurityPage's
  password-change form). Show "old links to your profile will break and the
  name becomes available to others" copy before confirming.
- Integration tests: rename happy path; taken name rejected; reserved name
  rejected; all sessions revoked after rename.

## Deployment order

**Heartbeat's provisioning eviction (its ADR-027) must be live before this
ships** — otherwise a re-claimed old name breaks the new owner's first
Heartbeat login on the local unique index.
