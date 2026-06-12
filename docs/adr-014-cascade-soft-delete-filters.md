# ADR-014: Cascade Soft-Delete Query Filters & Merge Completeness Guard

## Status: Accepted

## Date: 2026-06-12

(supersedes the "remember to filter in every query" consequence of [ADR-006](adr-006-soft-delete.md))

[`0075361`](https://github.com/shenxianovo/AuthService/commit/0075361) — fix(account): cover PasswordResets in merge, add completeness guard
[`76b8dda`](https://github.com/shenxianovo/AuthService/commit/76b8dda) — refactor(oauth): extract pure decision core from OAuthService
[`0deeffa`](https://github.com/shenxianovo/AuthService/commit/0deeffa) — refactor(data): enforce soft delete with cascade query filters

## Context

ADR-006 introduced soft delete with the consequence "must remember to filter
`IsDeleted` in every user query". That invariant was held up by human memory:
8 manual checks spread across 5 services, with nothing to catch the 9th query
that forgets. ADR-010 had already shown how invariants drift when they live in
many places (API keys orphaned by merge).

Reviewing those manual checks revealed that most were **unreachable**: a user is
only soft-deleted by an account merge, and merge migrates every owned relation
(providers, emails, sessions, API keys, password) to the target. A provider or
email row can therefore never point at a soft-deleted user. The defensive
branches guarding those states — and `AuthError.UserDeleted` behind them — could
only be tested by fabricating production-impossible data on the InMemory
provider, the same anti-pattern [ADR-011](adr-011-global-email-uniqueness.md)
removed. The one *reachable* path is a lookup by raw user id from a JWT that
outlives its merged-away user (ADR-001's 15-minute window).

`PasswordReset` was also discovered as dead-but-loaded schema: a table with an
FK to `User`, no application code, **no inverse navigation on `User`**, and no
merge handling — the exact setup that produced the ADR-010 incident.

## Decision

1. **Enforce soft delete with EF global query filters, cascaded.** `User`
   filters on `!IsDeleted`; every user-owned entity (`AuthProvider`,
   `UserEmail`, `Session`, `ApiKey`, `PasswordCredential`, `PasswordReset`)
   filters on `!x.User.IsDeleted`; two-level dependents (`RefreshToken`,
   `EmailVerification`) filter through their parent. A merged-away user and all
   its rows are invisible everywhere, by construction.
2. **Delete the manual checks and the unreachable defensive branches**, and
   remove `AuthError.UserDeleted`. The reachable case (stale JWT in the 15-min
   window) resolves as not-found / invalid, covered by real-path tests.
3. **Guard merge completeness structurally** (`MergeCompletenessGuardTests`):
   a manifest maps every FK→`User` entity type — discovered from the EF model,
   not from navigation properties — to its merge behavior (Moved / Recreated /
   Deleted). A new user-owned entity turns the test red until both
   `AccountService.MergeAsync` and the manifest handle it. The behavioral layer
   runs a full merge per relation against real PostgreSQL; the e2e layer covers
   the token paths over HTTP.
4. **Global-uniqueness checks bypass the filters.** Usernames stay occupied by
   soft-deleted rows under the unique index, so existence checks for username
   generation/registration use `IgnoreQueryFilters()`.

`PasswordReset` rows are deleted on merge (same spirit as revoking the source's
sessions); the entity stays because the password reset feature ships with
[ADR-015](adr-015-password-reset-and-change.md).

## Consequences

- ✅ The ADR-006 invariant is enforced by the ORM, not by review discipline —
  new queries are safe by default
- ✅ One regression class (forgotten filter → soft-deleted user authenticates)
  is structurally eliminated; another (new entity orphaned by merge) turns a
  test red at introduction time
- ✅ Dead branches and their fabricated-state tests are gone (ADR-011 discipline)
- ⚠️ Every query against user-owned tables joins `Users` for the filter —
  negligible at this service's scale
- ⚠️ Code that legitimately reads soft-deleted rows (merge internals, guard
  tests, uniqueness checks) must remember `IgnoreQueryFilters()` — the burden
  flipped from "every normal query" to "the few exceptional ones"
- ⚠️ `FindAsync` can return tracked soft-deleted entities (change-tracker hit
  bypasses filters); query-path code uses `FirstOrDefaultAsync` where that
  matters

## References

- [`UserConfiguration.cs`](../backend/AuthService/Data/Configurations/UserConfiguration.cs) — root filter; cascades in the sibling configurations
- [`MergeCompletenessGuardTests.cs`](../backend/AuthService.Tests/Integration/MergeCompletenessGuardTests.cs) — manifest + structural ratchet
- [`MergeTokenPathsTests.cs`](../backend/AuthService.Tests/Integration/MergeTokenPathsTests.cs) — post-merge token paths over HTTP
- [`OAuthResolver.cs`](../backend/AuthService/Services/OAuthResolver.cs) — pure decision core; facts can no longer contain soft-deleted users
