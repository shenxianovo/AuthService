# Delete the OIDC client seeder — database becomes source of truth

Status: ready-for-agent

## Context

Final slice of ADR-017. Depends on issue 02 (the UI must exist before the
config path dies). The seeder currently overwrites DB clients from config on
every boot, which would fight UI edits.

## Acceptance

- Delete `OidcClientSeeder`, the `Oidc:Clients` config section (appsettings +
  fixture), and `OPENLIST_CLIENT_SECRET` from compose.yml/.env.example.
  `Oidc:EncryptionKey` stays (infrastructure secret).
- Existing deployed OpenList registration survives (it's already a DB row;
  simply stop overwriting it). Flow integration tests create their client via
  the management API (or `IOpenIddictApplicationManager` directly) instead of
  fixture config.
- ADR-016 amended: client-seeding section marked superseded by ADR-017;
  ADR-017 status flipped to Accepted.

## References

- `backend/AuthService/Services/OidcClientSeeder.cs`
- `docs/adr-017-admin-role-and-ui-managed-oidc-clients.md`
