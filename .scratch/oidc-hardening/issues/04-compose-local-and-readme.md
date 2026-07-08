# compose.local.yml for full-stack local e2e + README overhaul

Status: done

## Context

Decided 2026-07-08: local end-to-end verification = the full compose stack
built from local sources (option b; no infra-only dev compose, no local
OpenList container). Day-to-day inner loop remains integration tests +
frontend mock mode. The README has no local development section at all and is
stale in several places.

## Acceptance

- `compose.local.yml` overriding `compose.yml`: `build:` contexts instead of
  ghcr images, dev-safe env values (dev DB password, dev `Oidc:EncryptionKey`),
  instructions for generating dev RSA keys into `./secrets/` (gitignored).
  Run: `docker compose -f compose.yml -f compose.local.yml up --build`.
- README gains a **Local development** section: test-first inner loop
  (`dotnet test`), frontend mock mode, full-stack e2e via compose.local.yml,
  OIDC smoke steps (discovery → authorize → token), NSwag client regeneration.
- README fixes: Deploy row systemd → docker compose (ADR-013); the
  per-ADR table replaced by a single link to `docs/` (no more sync burden);
  project-structure tree trimmed to folder level (same sync-burden reasoning);
  architecture diagram/mentions include the OIDC provider role.

## References

- `compose.yml`, `.env.example`, `README.md`
- `docs/adr-013-containerized-deployment.md`

## Comments

- 2026-07-08: resolved in commit cc8adf9, suite green.
