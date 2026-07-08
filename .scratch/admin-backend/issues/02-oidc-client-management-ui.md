# OIDC client management API + admin UI

Status: done

## Context

Second slice of ADR-017. Depends on issue 01 (RequireAdmin). Several
downstream services want SSO; registering them must become a UI action.

## Acceptance

- Admin-only CRUD API over `IOpenIddictApplicationManager`: list, create,
  update (display name / redirect URIs / type / scopes), delete.
- Secret lifecycle mirrors the API Key UX: generated server-side on create (and
  on explicit "regenerate"), displayed exactly once, hashed at rest by
  OpenIddict. No endpoint ever returns a stored secret.
- Client type selectable: confidential (secret) or public (no secret, PKCE
  requirement auto-applied — see oidc-hardening/03).
- Frontend: new dashboard page (admin-visible only) following the existing
  API Keys page patterns.
- Redirect URIs validated as absolute http(s) URLs; note in UI that matching
  is exact including query string (OpenList `?method=` variants).

## References

- `docs/adr-017-admin-role-and-ui-managed-oidc-clients.md`
- `backend/AuthService/Controllers/ApiKeyController.cs` + `frontend/src/pages/dashboard/ApiKeysPage.vue` — the UX to mirror

## Comments

- 2026-07-08: backend API in 69e7726, frontend page in 0bc116f, suite green (213).
