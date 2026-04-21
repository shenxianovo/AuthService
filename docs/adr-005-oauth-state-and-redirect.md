# ADR-005: OAuth State Signing + Redirect Whitelist

## Status: Accepted

## Date: 2026-03-20

[`5e133f9`](https://github.com/shenxianovo/AuthService/commit/5e133f9) — fix(security): replace token-in-URL with one-time auth code exchange, add signed state and redirect whitelist

## Context

OAuth `state` parameter is the standard CSRF protection for OAuth flows. A plain random string works, but doesn't carry context (where to redirect after login, which user is binding). Redirect URLs also need validation to prevent open-redirect attacks.

## Decision

- **State signing**: Use ASP.NET `DataProtection` to encrypt+sign the state payload (nonce, redirect URL, user ID, expiry). Tampered or expired states are rejected.
- **Redirect whitelist**: `AllowedRedirectOrigins` config supports exact match and wildcard subdomains (`https://*.shenxianovo.com`). Non-whitelisted URLs are rejected before initiating OAuth.

## Consequences

- ✅ CSRF protection with signed nonce — cannot be forged
- ✅ State carries structured data without a server-side lookup
- ✅ Open-redirect prevention via whitelist
- ⚠️ DataProtection key must be persisted across deployments (default: file system)

## References

- [`OAuthSecurityService.cs`](../backend/AuthService/Services/OAuthSecurityService.cs) — `GenerateState`, `ValidateState`, `ValidateRedirectUrl`
- [`OAuthSecurityOptions`](../backend/AuthService/Options/OAuthSecurityOptions.cs) — `AllowedRedirectOrigins`, `StateExpirationSeconds`
