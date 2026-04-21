# ADR-002: RS256 Asymmetric Signing over HS256

## Status: Accepted

## Date: 2026-03-14

[`e18cf48`](https://github.com/shenxianovo/AuthService/commit/e18cf48) — feat(JWT): add RSA keys

## Context

JWT signing algorithm choice. HS256 (symmetric) requires every verifier to hold the same secret. RS256 (asymmetric) uses a private/public key pair.

## Decision

Use **RS256** (RSA-SHA256):

- AuthService holds the **private key** (signs tokens)
- Downstream services only need the **public key** (verify tokens)

## Consequences

- ✅ Other services never touch the signing secret — reduced blast radius
- ✅ Public key can be freely distributed (even exposed via endpoint if needed)
- ✅ Adding a new service = copy `public.pem`, done
- ⚠️ RSA key pair must be generated and managed (see `Keys/`)
- ⚠️ Slightly larger token size and slower signing vs HS256 (negligible in practice)

## References

- [`JwtService.cs`](../backend/AuthService/Services/JwtService.cs) — loads RSA keys, signs/verifies
- [`Keys/`](../backend/AuthService/Keys/) — RSA key pair (private.pem, public.pem)
