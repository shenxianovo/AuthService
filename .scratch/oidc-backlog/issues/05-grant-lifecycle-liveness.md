# OIDC grant lifecycle: liveness check on refresh + revoke on delete/merge

Status: done

## Context

Decided 2026-07-09 (grilling session): downstream RP logins follow the Google
model — logging out of AuthService does **not** end them — but a soft-deleted
or merged user must not retain working OIDC credentials.

Today neither half is enforced:

- The token endpoint has no passthrough, so `grant_type=refresh_token` is
  handled entirely inside OpenIddict: it re-issues tokens from the stored
  principal with no check against `Users.IsDeleted`. A merged-away source
  account keeps refreshing OpenList tokens until the refresh token expires.
- `AccountService.MergeAsync` / soft-delete never touches the OpenIddict
  tables, even though OIDC grants are part of the account composition
  (CONTEXT.md, updated 2026-07-09).

## Acceptance

- `EnableTokenEndpointPassthrough()` + a token controller action (Velusia
  pattern): for `authorization_code` and `refresh_token` grants, authenticate
  the OpenIddict scheme, re-check the user exists and `!IsDeleted`, reject
  with `invalid_grant` otherwise, then `SignIn` with the retrieved principal.
- `AccountService` revokes the source user's OpenIddict authorizations and
  tokens (by `sub`) on merge and on soft-delete, in the same unit of work —
  AccountService stays the single write authority (ADR-010).
- Integration tests:
  - refresh succeeds after the originating session is revoked (documents the
    decided logout semantic);
  - refresh fails after the user is soft-deleted;
  - merge revokes the source user's grants, target user unaffected.
- ADR-016 consequence line already corrected (2026-07-09).

## Notes

- Future "connected apps" management page can list/revoke grants per `sub`
  straight from `OpenIddictAuthorizations` — no new storage needed.
- A token revocation endpoint (`SetRevocationEndpointUris`) is a natural
  companion but not required by this issue; fold it into the end-session work
  (issue 03) if OpenList ever needs it.

## Comments

- 2026-07-13: resolved — token endpoint passthrough with liveness check
  (`AuthorizationController.Exchange`), `OidcGrantRevoker` called from
  `AccountService.MergeAsync`, three lifecycle integration tests
  (`GrantLifecycleTests`) plus the merge-guard pin. Suite green (211).
