# Unify token issuance into OpenIddict — SPA dogfoods OIDC, JwtService retires

Status: open (backlog — explicitly deferred 2026-07-17, do not couple to the
username-rename work)

## Context

Two token-issuance lines exist today, both minting RS256 JWTs with the same
key but with separate lifetimes, refresh mechanisms and revocation stories:

| | long-lived credential | short-lived token | consumers |
|---|---|---|---|
| hand-rolled | DB `Session` (30 d) + rotating `RefreshToken` | 15-min session JWT (`sid` claim) | own SPA, Heartbeat agent (API-key exchange) |
| OpenIddict | encrypted refresh token (grant lifecycle) | 1-h access token (`typ: at+jwt`) | OIDC RPs (OpenList, Heartbeat web) |

This is not accidental duplication — an IdP necessarily has its own login
state, and the OpenIddict line already rides on it (`/connect/authorize`
re-validates `sid` against `Sessions` on every request, ADR-016). The
unification is a **narrowing**, not an elimination: the hand-rolled line
shrinks to pure login state (Session + Interactive cookie), and all token
issuance converges on OpenIddict.

## Shape of the work

- SPA becomes the first first-party OIDC client: authorization code flow
  against our own endpoints; login endpoints keep creating Session +
  Interactive cookie but stop returning JWTs directly.
- API-key exchange becomes an OpenIddict custom grant (`AllowCustomFlow`),
  so Heartbeat agents also receive OpenIddict-issued tokens.
- `SetAccessTokenLifetime(TimeSpan.FromMinutes(15))` — fold in the deferred
  lifetime alignment (decided 2026-07-17 to wait for this project rather
  than patch it standalone).
- End state: one issuance path, one refresh semantic, one revocation story;
  `JwtService` and the hand-rolled refresh rotation retire.

## Notes

- Downstream JWKS verification is unaffected (same key, same
  `/.well-known/jwks.json` — ADR-016 pinned by regression test).
- Heartbeat's dual-scheme JWT validation (routes by `typ` header) simplifies
  to one scheme once agents carry `at+jwt` tokens — coordinate the cutover.
- Big migration (SPA auth rewrite + agent exchange rewrite + cookie flow
  adjustments); needs its own ADR when picked up.
