# ADR-013: Containerized Deployment with Docker Compose

## Status: Accepted

## Date: 2026-06-03

## Context

AuthService was deployed via systemd: CI published the .NET output, SCP'd a
timestamped release directory to the server, ran an EF Core migration bundle
against a bare-metal PostgreSQL, atomically switched a `current` symlink, and
`systemctl restart`ed the service. The frontend followed the same release-dir +
symlink pattern under nginx. RSA signing keys lived at `/srv/keys/AuthService/`
and were copied into each release. PostgreSQL ran bare-metal on the host.

This worked but coupled the deployment to host state: the .NET runtime, the
systemd unit, the nginx layout, and the database all had to be provisioned and
kept in sync by hand. Reproducing the environment elsewhere (or recovering after
host loss) meant re-deriving all of that from memory. We wanted a single,
declarative description of the running service.

## Decision

Run the service as containers orchestrated by a single `docker-compose.yml` on
the same single host:

- **Three services**: `db` (postgres:18.4-alpine, named volume), `backend`
  (.NET 10, Kestrel on 8080), `frontend` (nginx:alpine serving the SPA and
  reverse-proxying `/api`, `/.well-known`, `/health` to `backend`).
- **Images** are built by GitHub Actions and pushed to GHCR as public images,
  tagged `latest` + `sha-<commit>`. Deploy = SSH in, `docker compose pull && up -d`.
- **Migrations** run on backend startup via `MigrateAsync()` (single instance,
  so no migration race). The CI `efbundle` step is removed.
- **Config** is injected through a hand-maintained `.env` on the server (.NET's
  `__` env-var convention maps to config sections). RSA keys are mounted as
  docker secrets at `/run/secrets/`, requiring no code change since
  `PemFileRsaKeyProvider` already reads a configurable path.
- **Host nginx** is demoted to TLS termination + a single `proxy_pass` to the
  frontend container. All API routing moves into the frontend container's nginx.
  The host `auth.conf` is synced by the legacy QuQLab deploy repo, which already
  owns host nginx — keeping a single management entrypoint for the host's nginx.
- **Database** is containerized; the bare-metal PG 15 instance is dumped and
  restored into the `db` volume on PG 18, then retired.

## Consequences

- ✅ The entire runtime is declared in one compose file — reproducible, host-agnostic.
- ✅ Deploys are image pulls; rollback = pin a `sha-` tag in compose.
- ✅ Secrets stay off the image and out of `docker inspect` env (docker secrets).
- ✅ The service owns its own runtime (compose/Dockerfiles); host nginx routing
  stays in QuQLab, which already manages the host's nginx.
- ⚠️ Startup migration means a bad migration blocks the container from starting —
  visible in `docker logs`, but there is no separate gate. Acceptable at single instance.
- ⚠️ One extra proxy hop (host nginx → frontend container → backend). Negligible on one host.
- ⚠️ The database now lives in a docker named volume; backups must target the
  volume (or `pg_dump` from the container), not a host PG data dir.
- ⚠️ Initial bootstrap (compose.yml, .env, secrets/, auth.conf) is manual on the
  server; only image updates are automated by CI.

## References

- [`docker-compose.yml`](../docker-compose.yml) — service orchestration
- [`Program.cs`](../backend/AuthService/Program.cs) — startup `MigrateAsync()`
- [`backend/Dockerfile`](../backend/Dockerfile), [`frontend/Dockerfile`](../frontend/Dockerfile)
- Host nginx entrypoint `auth.conf` lives in the QuQLab deploy repo
