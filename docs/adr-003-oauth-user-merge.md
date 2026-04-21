# ADR-003: OAuth Login User Merge Strategy

## Status: Accepted

## Date: 2026-03-20

[`51aac54`](https://github.com/shenxianovo/AuthService/commit/51aac54) — feat(auth): add account merging and user info endpoint
[`c674ca5`](https://github.com/shenxianovo/AuthService/commit/c674ca5) — feat(auth): revoke sessions of source user during account merging

## Context

When a user logs in via OAuth (GitHub/Google), several identity conflicts can arise:

1. New provider, new email → create new user
2. New provider, email matches existing user → link to existing
3. Binding flow: user is logged in, connects a new OAuth provider that belongs to a different existing user → need to merge

Ignoring case 3 leads to orphaned accounts and confusion.

## Decision

Implement **user merge** when binding triggers a conflict:

- All `AuthProviders`, `UserEmails`, `PasswordCredentials`, `Sessions` are moved from the source user to the target user
- Duplicate emails are deduplicated (keep target's, delete source's)
- Source user is soft-deleted (`IsDeleted = true`)
- Source's active sessions are revoked

## Consequences

- ✅ No orphaned accounts — users can always consolidate
- ✅ Idempotent: binding an already-linked provider to the same user is a no-op
- ⚠️ Merge is irreversible (soft delete only)
- ⚠️ Complex logic with many edge cases (covered by tests)

## References

- [`OAuthService.cs`](../backend/AuthService/Services/OAuthService.cs) — `ProcessOAuthLoginAsync`, `MergeUserAsync`
