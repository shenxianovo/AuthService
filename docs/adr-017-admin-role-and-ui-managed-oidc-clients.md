# ADR-017: Admin Role and UI-Managed OIDC Clients

## Status: Proposed

## Date: 2026-07-08

(Not yet implemented — this records the design agreed for the upcoming admin backend feature.)

## Context

Multiple downstream services now want to integrate via SSO ([ADR-016](adr-016-openiddict-oidc-provider.md)),
and managing each client's secret and redirect URIs through `.env` +
`appsettings.json` does not scale operationally. An admin backend is planned.
Two prerequisites need decisions: how admin authority is modeled, and who owns
OIDC client registrations once a UI exists (the config seeder overwrites the
database on every boot, so UI edits and config would fight).

## Decision

1. **`Role` enum on `User` (`User` / `Admin`)**, bootstrap admin designated by
   deployment configuration (idempotent promotion at startup, same pattern as
   other seeding). Not first-user-auto-admin: registration is public.
2. **Role is never emitted in any token or OIDC claim.** Admin endpoints use an
   authorization policy that consults the user record per request. This makes
   grants/revocations immediate and — more importantly — makes it structurally
   impossible for downstream services to build authorization on AuthService
   roles. Downstream permission systems (membership tiers etc.) live in the
   downstream service's own data, keyed on `sub` (see `Role` in
   [CONTEXT.md](../CONTEXT.md)).
3. **The database becomes the sole source of truth for OIDC clients.** The
   `OidcClientSeeder`, the `Oidc:Clients` configuration section and the
   `OPENLIST_CLIENT_SECRET` env var are deleted when the client-management UI
   ships. Clients are created in the admin UI; secrets are generated
   server-side, displayed once, and hashed at rest (mirroring the API Key UX).
   `Oidc:EncryptionKey` stays in the environment — infrastructure secret, not
   client data.

## Consequences

- ✅ Registering a new downstream service becomes a UI action, no redeploy.
- ✅ Role separation between AuthService and downstream services is enforced by
  architecture (claim absent), not by convention.
- ✅ Admin revocation is instant; no token-lifetime window.
- ⚠️ One DB query per admin request (negligible at admin-panel traffic).
- ⚠️ Partially supersedes ADR-016's client-seeding section once implemented.
- ⚠️ Rebuilding a lost database requires re-creating clients by hand — accepted,
  since a lost database loses the user table too.

## References

- [`OidcClientSeeder.cs`](../backend/AuthService/Services/OidcClientSeeder.cs) — to be deleted by this ADR
- [`ApiKeyController.cs`](../backend/AuthService/Controllers/ApiKeyController.cs) — the secret-shown-once UX to mirror
