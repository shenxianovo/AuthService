# Rate limiting on credential-bearing endpoints

Status: needs-triage

## Context

`/connect/token` (client_secret brute force), `/api/v1/auth/login` (password
brute force), `/api/v1/apikeys/exchange` (key brute force) have no rate
limiting. Pre-existing gap, not introduced by OIDC; low urgency for a
self-hosted single-operator service with strong random secrets.

## Sketch

ASP.NET Core built-in RateLimiter middleware, fixed-window per IP on the
endpoints above. Respect `UseForwardedHeaders` ordering so the real client IP
is used behind nginx.
