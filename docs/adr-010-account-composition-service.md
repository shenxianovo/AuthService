# ADR-010: Account Composition Service & Merge Completeness

## Status: Accepted

## Date: 2026-05-14

(supersedes the data-migration scope of [ADR-003](adr-003-oauth-user-merge.md))

## Context

Account writes were spread across `OAuthService` (a 190-line nested-branch method
that created users, linked providers, and merged accounts inline),
`PasswordAuthService` (user creation), and `UserService` (provider unlink). There
was no single authority for "what data constitutes a user account."

This caused a concrete bug: when API keys were added (ADR-009, 2026-04-22), the
merge logic written for ADR-003 (2026-03-20) was never updated to migrate them.
Merging an account silently orphaned the source user's API keys on the
soft-deleted user — they neither moved to the target nor appeared in the
dashboard, and `ApiKeyService.ExchangeAsync` rejected them via the `IsDeleted`
check. ADR-003 promised "no orphaned accounts" but the promise had drifted out of
sync with the entity model.

## Decision

Introduce **`AccountService`** as the single write authority for account
composition. It owns every operation that creates or mutates the set of data
making up an account: emails, providers, password, sessions, API keys.

- `OAuthService` becomes a pure **decision layer**: it queries to resolve which
  account a login maps to, then delegates the write to `AccountService` and
  commits once.
- `MergeAsync` migrates **all** relations in one place — `AuthProviders`,
  `UserEmails` (deduplicated), `Sessions`, **`ApiKeys`**, `PasswordCredential` —
  then soft-deletes the source. Adding a new relation type now has exactly one
  place that must be updated.
- `AccountService` write methods **do not** call `SaveChangesAsync`. Callers
  (`OAuthService`, `PasswordAuthService`, `UserController`) commit once, so a full
  OAuth login or merge is a single atomic transaction.
- Provider unlink moved from `UserService` to `AccountService`; `UserService` is
  now a pure query service.

## Consequences

- ✅ Merge completeness is structural: one relation list, traversed once. The
  ApiKey orphan bug is fixed and locked by a regression test.
- ✅ Account creation is reused by OAuth and password registration (no duplication).
- ✅ OAuth login is a single transaction — no half-merged state on mid-operation failure.
- ✅ Decision logic (OAuthService) and write logic (AccountService) test independently.
- ⚠️ Callers are responsible for `SaveChangesAsync`. Write-method names carry
  staging semantics to signal this.
- ⚠️ Merge remains irreversible (soft delete only), unchanged from ADR-003.

## References

- [`IAccountService.cs`](../backend/AuthService/Services/IAccountService.cs) — the write authority contract
- [`AccountService.cs`](../backend/AuthService/Services/AccountService.cs) — `MergeAsync` migrates all relations incl. ApiKeys
- [`OAuthService.cs`](../backend/AuthService/Services/OAuthService.cs) — decision layer delegating to AccountService
- [`AccountServiceTests.cs`](../backend/AuthService.Tests/Unit/Services/AccountServiceTests.cs) — `Merge_MigratesApiKeysToTarget` regression
