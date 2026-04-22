# ADR-009: API Key Issuance + Exchange for Short-Lived JWT

## Status: Accepted

## Date: 2026-04-22

[`64d668c`](https://github.com/shenxianovo/AuthService/commit/64d668c273566fe723c61f56479bcb57cc76e9ee) — feat: add API Key issuance and exchange for short-lived JWT

## Context

Downstream services like Heartbeat need a way for users to authenticate long-running agents (e.g. a daemon reporting metrics every 5 seconds). Session-based login (browser cookies / refresh tokens) doesn't fit — agents are headless and need a credential they can store in a config file.

We need a mechanism that:
- Lets users create long-lived credentials from the AuthService dashboard
- Keeps downstream services stateless (no shared DB, no per-request call to AuthService)
- Supports immediate revocation when the user deletes a key

## Decision

**API Key + JWT exchange pattern:**

1. **Create**: User creates a named API Key via `POST /api/v1/apikeys`. The full key (`ak_<prefix>_<secret>`) is returned only once; the database stores only the SHA-256 hash of the secret and an 8-char prefix for lookup.
2. **Exchange**: The agent calls `POST /api/v1/apikeys/exchange` with the raw key, receives a short-lived JWT access token (same RS256 signing as session-based tokens, with an `akid` claim instead of `sid`).
3. **Verify**: Downstream services verify the JWT offline using the AuthService public key — zero network overhead per request.
4. **Revoke**: User revokes the key via `DELETE /api/v1/apikeys/{id}` (soft delete, `IsRevoked = true`). The next exchange attempt fails; already-issued JWTs expire naturally.

Key format: `ak_<8-char prefix>_<URL-safe Base64 of 32 random bytes>`

Storage: prefix (indexed) + SHA-256 hash of secret. Lookup is prefix → candidate row → constant-time hash comparison.

### Alternatives Considered

| Approach | Rejected because |
|----------|-----------------|
| Raw API Key verified per-request by calling AuthService | High-frequency agents (every 5s) would create too much load; couples downstream services to AuthService availability |
| API Key as self-contained JWT (long-lived) | Cannot be revoked until expiry; long-lived JWTs are a security risk if leaked |
| Shared database between services | Violates service boundary; tight coupling |

## Consequences

- ✅ Agents only call AuthService once per token lifetime (e.g. every 15 min), then use JWT for all requests
- ✅ Downstream services remain fully stateless — public key verification only
- ✅ Immediate revocation prevents new token issuance; exposure window limited to current JWT lifetime
- ✅ Key prefix enables fast DB lookup without exposing the secret
- ⚠️ After revocation, already-issued JWTs remain valid until expiry (acceptable trade-off for stateless verification)
- ⚠️ Exchange endpoint is unauthenticated — should be rate-limited in production

## References

- [`ApiKey.cs`](../backend/AuthService/Entities/ApiKey.cs) — entity
- [`ApiKeyService.cs`](../backend/AuthService/Services/ApiKeyService.cs) — create, list, revoke, exchange logic
- [`ApiKeyController.cs`](../backend/AuthService/Controllers/ApiKeyController.cs) — HTTP endpoints
- [`JwtService.cs`](../backend/AuthService/Services/JwtService.cs) — unified `GenerateAccessToken(userId, params Claim[])` interface
