# ADR-001: Session + JWT Hybrid Architecture

## Status: Accepted

## Date: 2026-03-13

Initial password auth with session: [`a6ed08c`](https://github.com/shenxianovo/AuthService/commit/a6ed08c)
Extract SessionService: [`fca556f`](https://github.com/shenxianovo/AuthService/commit/fca556f)

## Context

This service is a central auth hub for multiple services under `*.shenxianovo.com`. We need:

- A controllable login session (force logout, revoke)
- Stateless identity tokens for downstream services (no shared DB)

## Decision

- **AuthService itself** maintains sessions in PostgreSQL (stateful, controllable)
- **Issues RS256 JWT access tokens** for other services (stateless, verify with public key only)
- Access token TTL: 15 minutes. Refresh token for renewal.

## Consequences

- ✅ Session revocation works (delete session → refresh fails)
- ✅ Downstream services are fully decoupled (no DB dependency)
- ⚠️ Access tokens remain valid until expiry (max 15min window after revocation)
- ⚠️ Higher complexity than pure session or pure JWT alone

## References

- [`SessionService.cs`](../backend/AuthService/Services/SessionService.cs) — session + refresh token management
- [`JwtService.cs`](../backend/AuthService/Services/JwtService.cs) — RS256 token generation
