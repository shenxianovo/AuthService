# ADR-012: Trust Provider `email_verified` for Email Verification

## Status: Accepted

## Date: 2026-05-30

_Pending commit_ — feat(oauth): thread provider email_verified into VerifiedAt

## Context

A user can register with email+password (the email is created **unverified**) and later
log in via OAuth returning that same address. Two problems existed in how the OAuth flow
set `UserEmail.VerifiedAt`:

1. **Asymmetry.** The binding flow (`AddProviderAsync` with an email) auto-set
   `VerifiedAt`, but the pure-login path (Case 3 in `OAuthService.ResolveAsync`) passed
   `email: null` and so never upgraded an existing unverified email — even though the
   provider had just authenticated the same address.
2. **Unconditional trust.** Where it did set `VerifiedAt`, it did so regardless of
   whether the provider actually asserted the email was verified. The normalized
   `OAuthUserInfo` carried no verification flag at all — Google's `email_verified` and
   GitHub's per-address `verified` were fetched-then-discarded (GitHub's wasn't fetched).
   Trusting an unverified provider email is a real account-takeover vector, since email
   match is also what drives account linking (Case 3) and merge.

`VerifiedAt` is load-bearing: `EmailManagementService.SetPrimaryEmailAsync` refuses to
promote an unverified email (`EmailNotVerified`).

## Decision

Thread a provider-asserted `emailVerified` flag through the whole OAuth flow and set
`VerifiedAt` **only when the provider asserts the address is verified**:

- `OAuthUserInfo` gains `EmailVerified`. Each provider populates it from its authoritative
  source: Google from the userinfo `email_verified` claim; GitHub from the `verified`
  field of the primary entry in `GET /user/emails` (the `/user` profile email carries no
  verification status, so the email itself is now also sourced from `/user/emails`).
- `ProcessOAuthLoginAsync` → `ResolveAsync` → `AccountService` carry the flag.
  `CreateFromOAuthAsync` and `AddProviderAsync` set `VerifiedAt` only when it is true.
- Case 3 (pure login, email already on the user) now passes the real email instead of
  `null`. Because the address is globally unique ([ADR-011](adr-011-global-email-uniqueness.md)),
  the existing row is found rather than duplicated, and its `VerifiedAt` is upgraded when
  the provider asserts verification — closing the asymmetry.

## Consequences

- ✅ A password user who logs in with a provider-verified OAuth email gets their email
  upgraded to verified, regardless of login vs binding path.
- ✅ An unverified provider email never silently marks an address verified.
- ⚠️ GitHub login now makes a second API call (`/user/emails`). Required anyway, since
  `/user` may hide the email and never reports verification.
- ⚠️ We are trusting the provider's assertion. That is the intended trust boundary: if a
  provider lies about `email_verified`, that provider is compromised — out of scope.

## References

- [`OAuthProviderServiceBase.cs`](../backend/AuthService/Services/OAuthProviderServiceBase.cs) — `OAuthUserInfo.EmailVerified`
- [`GithubAuthService.cs`](../backend/AuthService/Services/GithubAuthService.cs) — verified primary from `/user/emails`
- [`GoogleAuthService.cs`](../backend/AuthService/Services/GoogleAuthService.cs) — `email_verified` claim
- [`AccountService.cs`](../backend/AuthService/Services/AccountService.cs) — `CreateFromOAuthAsync` / `AddProviderAsync` gate `VerifiedAt`
- [`OAuthService.cs`](../backend/AuthService/Services/OAuthService.cs) — Case 3 upgrades existing email verification
