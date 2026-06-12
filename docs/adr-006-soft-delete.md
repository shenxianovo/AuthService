# ADR-006: Soft Delete Users Instead of Hard Delete

## Status: Accepted (enforcement superseded by [ADR-014](adr-014-cascade-soft-delete-filters.md))

## Date: 2026-03-20

[`51aac54`](https://github.com/shenxianovo/AuthService/commit/51aac54) — feat(auth): add account merging and user info endpoint

## Context

Account merging moves all data from a source user to a target user. The source user becomes empty. Two options: physically delete or soft-delete.

## Decision

Soft delete via `User.IsDeleted` flag:

- Merged source users are marked `IsDeleted = true`
- All queries filter out deleted users — _enforcement superseded by
  [ADR-014](adr-014-cascade-soft-delete-filters.md): cascade EF global query
  filters replaced the per-service manual checks_
- Row remains in DB for audit trail

## Consequences

- ✅ Audit trail preserved — can trace merge history
- ✅ Foreign keys remain intact (no cascade delete complications)
- ✅ Reversible in theory (clear the flag)
- ⚠️ ~~Must remember to filter `IsDeleted` in every user query~~ — closed by
  ADR-014's query filters

## References

- [`User.cs`](../backend/AuthService/Entities/User.cs) — `IsDeleted` property
- [`OAuthService.cs`](../backend/AuthService/Services/OAuthService.cs) — `MergeUserAsync` sets `IsDeleted = true`
