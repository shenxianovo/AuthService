# Emit only the verified primary email in OIDC claims

Status: done

## Context

The id_token/userinfo currently include `email` + `email_verified: false` for
unverified addresses. Downstream RPs are off-the-shelf software (OpenList) that
may not check `email_verified` — an attacker can register with someone else's
email (registration doesn't require verification) and SSO into a downstream
carrying that address. Mirror of the trust boundary ADR-012 already enforces
on the upstream side. Decided 2026-07-08: option (a) — omit unverified emails
entirely.

## Acceptance

- `AuthorizationController` and `UserinfoController` emit `email` /
  `email_verified` **only when the primary email has `VerifiedAt != null`**.
- Remove the `?? user.Emails.FirstOrDefault()` fallback — only the verified
  primary counts; no email claim otherwise.
- Integration test: unverified registration → id_token and userinfo carry no
  email claim; after verification the claims appear.
- Update ADR-016's claim-mapping section to record this rule.

## References

- `backend/AuthService/Controllers/AuthorizationController.cs`
- `backend/AuthService/Controllers/UserinfoController.cs`
- `docs/adr-012-oauth-email-verification-trust.md` — the mirrored reasoning

## Comments

- 2026-07-08: resolved in commit f103c66, suite green.
