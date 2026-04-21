# ADR-006: Soft Delete Users Instead of Hard Delete

## Status: Accepted

## Date: 2026-03-20

[`51aac54`](https://github.com/shenxianovo/AuthService/commit/51aac54) — feat(auth): add account merging and user info endpoint

## Context

Account merging moves all data from a source user to a target user. The source user becomes empty. Two options: physically delete or soft-delete.

## Decision

Soft delete via `User.IsDeleted` flag:

- Merged source users are marked `IsDeleted = true`
- All queries filter out deleted users (`IsDeleted` checked in services)
- Row remains in DB for audit trail

## Consequences

- ✅ Audit trail preserved — can trace merge history
- ✅ Foreign keys remain intact (no cascade delete complications)
- ✅ Reversible in theory (clear the flag)
- ⚠️ Must remember to filter `IsDeleted` in every user query

## References

- [`User.cs`](../backend/AuthService/Entities/User.cs) — `IsDeleted` property
- [`OAuthService.cs`](../backend/AuthService/Services/OAuthService.cs) — `MergeUserAsync` sets `IsDeleted = true`
