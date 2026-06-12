# ADR-015: Password Reset & Change Flows

## Status: Accepted

## Date: 2026-06-12

[`f55d111`](https://github.com/shenxianovo/AuthService/commit/f55d111) — feat(auth): add password reset, change and set flows
[`d28d25c`](https://github.com/shenxianovo/AuthService/commit/d28d25c) — feat(frontend): add forgot/reset password pages and change-password form

## Context

The `PasswordResets` table existed since the initial migration but had no
application code — a loaded gun for the ADR-010 drift class. Implementing the
feature raised the design question: what proves a user's identity for each
password operation? Following Google's account model (and unlike Microsoft's,
which leaves stale sessions alive for up to 24h after a credential change),
three operations exist with three distinct proofs.

## Decision

| Flow | Auth state | Proof | Session policy |
|---|---|---|---|
| **Password reset** (forgot) | anonymous | control of a **verified** mailbox | revoke **all** sessions + void other reset links |
| **Password change** | authenticated | the **current password** — a session alone is not enough | revoke all **other** sessions, keep the caller's `sid` |
| **Password set** | authenticated | session, when no credential exists | none (pre-existing add-password flow) |

Mechanics:

- **Reset token** follows ADR-007/009: 32 random bytes (base64url) in the
  emailed link, only the SHA-256 hex stored (unique-indexed), 30-minute TTL,
  single use. All failure modes collapse into one `InvalidResetToken` error.
- **Anti-enumeration**: `forgot-password` always returns 204 — unknown,
  unverified and rate-limited addresses are silently ignored (a 429 would leak
  existence). Only **verified** emails qualify, because `VerifiedAt` is
  load-bearing (ADR-012) and an unauthenticated reset against an unverified
  address is an account-takeover vector.
- **OAuth-only accounts**: reset sets a first password via the same
  `AccountService` write — mailbox proof is equally strong there.
- **Current-password requirement** on change exists because a stolen session
  (or an unattended machine) must not be enough to take over the credential.
  API-key JWTs (`akid`, no `sid`) are rejected outright.
- Password writes go through `AccountService.SetPasswordAsync` (ADR-010 write
  authority); revocation is the non-committing
  `SessionService.RevokeAllSessionsAsync`, so credential change + sign-out land
  in one atomic commit.
- Pending resets of a merge's source user are deleted (ADR-014 manifest:
  `Deleted`), and the cascade filters hide a merged-away user's reset rows.

## Consequences

- ✅ Each flow's proof matches its threat model; stolen sessions cannot rotate
  the credential, stolen passwords are evicted everywhere on reset
- ✅ Email enumeration is not possible through the reset endpoint
- ✅ DB breach does not leak usable reset tokens (hash-only storage)
- ⚠️ Reset requires a verified email — users who never verified must use a
  still-working login method (or support)
- ⚠️ Access tokens issued before a reset stay valid up to 15 minutes (ADR-001
  window)

## References

- [`PasswordResetService.cs`](../backend/AuthService/Services/PasswordResetService.cs) — token lifecycle, anti-enumeration
- [`PasswordAuthService.cs`](../backend/AuthService/Services/PasswordAuthService.cs) — `ChangePasswordAsync`
- [`AccountService.cs`](../backend/AuthService/Services/AccountService.cs) — `SetPasswordAsync`
- [`PasswordAuthController_ResetAndChangeTests.cs`](../backend/AuthService.Tests/Integration/Controllers/PasswordAuthController_ResetAndChangeTests.cs) — full HTTP flows
