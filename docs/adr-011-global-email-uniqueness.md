# ADR-011: Global Email Uniqueness

## Status: Accepted

## Date: 2026-05-30

_Pending commit_ — refactor(account): document global email uniqueness, remove unreachable merge dedup

## Context

`UserEmail.Email` carries an unconditional unique index (`UserEmailConfiguration`,
`HasIndex(e => e.Email).IsUnique()`) — unique on the address alone, not scoped to a
user and not filtered by soft-delete. Emails are normalized to lower case before
persistence everywhere they are written.

This was an implicit schema decision whose consequences had leaked into application
logic without being named:

1. `AccountService.MergeAsync` carried a dedup branch that removed a source email when
   the target already held the same address. Under the global unique index that state
   is unreachable — two users can never hold the same email — so the branch was dead
   code. An InMemory unit test (`Merge_DeduplicatesSharedEmails`) "passed" only because
   the InMemory provider ignores unique indexes, asserting a production-impossible state.
2. `EmailManagementService.AddEmailAsync` already enforces the rule explicitly with a
   global existence check returning `EmailAlreadyExists`, confirming the uniqueness is
   intended product behavior, not an accident of indexing.

## Decision

Treat **global email uniqueness as a deliberate, load-bearing invariant**: an email
address belongs to at most one user across the whole system.

Consequences for the code:

- `MergeAsync` reassigns every source email to the target unconditionally (no dedup is
  possible). The moved emails lose primary status; the target keeps its own primary.
- The merge path's reliance on this invariant is validated against real PostgreSQL in
  `MergeConstraintTests`, not just InMemory — the unique index is exactly the kind of
  constraint the InMemory provider silently ignores (see [ADR-010](adr-010-account-composition-service.md)).

This supersedes the "duplicate emails are deduplicated" statement in
[ADR-003](adr-003-oauth-user-merge.md), which described behavior that the schema makes
unreachable.

## Consequences

- ✅ Merge logic is simpler and honest — no defensive branch for an impossible state.
- ✅ Tests assert reachable states only; the invariant itself is covered by a real-DB
  constraint test.
- ⚠️ Two distinct humans cannot register the same address (e.g. a shared family inbox).
  This is accepted: account identity is keyed on email.
- ⚠️ If the product ever needs per-user emails (same address on multiple accounts), this
  invariant — and the dedup logic removed here — must be reintroduced deliberately, not
  by relaxing the index alone.

## References

- [`UserEmailConfiguration.cs`](../backend/AuthService/Data/Configurations/UserEmailConfiguration.cs) — the global unique index
- [`AccountService.cs`](../backend/AuthService/Services/AccountService.cs) — `MergeAsync` email reassignment
- [`EmailManagementService.cs`](../backend/AuthService/Services/EmailManagementService.cs) — `AddEmailAsync` global existence check
- [`MergeConstraintTests.cs`](../backend/AuthService.Tests/Integration/MergeConstraintTests.cs) — real-DB validation
