# ADR-004: Auth Code Exchange Instead of Direct Token Return

## Status: Accepted

## Date: 2026-03-20

[`5e133f9`](https://github.com/shenxianovo/AuthService/commit/5e133f9) — fix(security): replace token-in-URL with one-time auth code exchange, add signed state and redirect whitelist

## Context

After OAuth callback, the server has a valid user identity and needs to return tokens to the client. Two approaches:

1. **Direct**: Redirect with tokens in URL fragment/query (`?token=xxx`)
2. **Auth code**: Redirect with a one-time code; client POSTs to exchange it for tokens

## Decision

Use **auth code exchange** (approach 2):

- OAuth callback generates a short-lived one-time code, stores the payload in `IMemoryCache`
- Client redirected to frontend with `?code=xxx`
- Frontend POSTs `code` to `/api/v1/auth/exchange` → receives access + refresh tokens

## Consequences

- ✅ Tokens never appear in URLs (URLs end up in browser history, server logs, referrer headers)
- ✅ Code is single-use and short-lived (seconds)
- ✅ Mirrors standard OAuth2 Authorization Code Flow
- ⚠️ `IMemoryCache` is single-instance; multi-instance deployment needs distributed cache

## References

- [`OAuthSecurityService.cs`](../backend/AuthService/Services/OAuthSecurityService.cs) — `GenerateAuthCode`, `ConsumeAuthCode`
- [`ExchangeController.cs`](../backend/AuthService/Controllers/ExchangeController.cs) — `/api/v1/auth/exchange`
- [`OAuthController.cs`](../backend/AuthService/Controllers/OAuthController.cs) — callback handlers
