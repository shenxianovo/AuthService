# ADR-007: Refresh Token Rotation + Hash Storage

## Status: Accepted

## Date: 2026-04-13

[`1342643`](https://github.com/shenxianovo/AuthService/commit/1342643) — feat: add refresh token rotation and logout endpoints

## Context

Long-lived refresh tokens are a high-value target. If stolen, an attacker can mint access tokens indefinitely.

## Decision

- **Rotation**: Every refresh issues a new refresh token; the old one is immediately revoked.
- **Hash-only storage**: Only `SHA256(token)` is stored in DB. Raw token is returned to the client once and never persisted.
- **Replay detection**: Reusing a revoked refresh token fails with `InvalidRefreshToken`.

## Consequences

- ✅ Stolen token has a single-use window (attacker or real user, one wins)
- ✅ DB breach doesn't leak usable tokens (hashes only)
- ✅ Each session tracks its full token rotation history
- ⚠️ Client must store the latest refresh token reliably; losing it means re-login

## References

- [`SessionService.cs`](../backend/AuthService/Services/SessionService.cs) — `RefreshSessionAsync`, `CreateSessionAsync`
- [`RefreshToken.cs`](../backend/AuthService/Entities/RefreshToken.cs) — entity with `TokenHash` field
