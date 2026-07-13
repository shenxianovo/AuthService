# Context

Domain glossary for AuthService — the authoritative identity/auth service. Other
services (e.g. Heartbeat) depend on the terms defined here. When naming a concept
in code, tests, issues, or ADRs, use the term as defined below; avoid the listed
synonyms.

## Glossary

### Account composition
The full set of data that constitutes a user: the `User` row plus its emails,
auth providers, password credential, sessions, API keys, and OIDC grants
(OpenIddict authorization/token rows keyed on `sub`). `AccountService` is
the single write authority over this set (see [ADR-010](docs/adr-010-account-composition-service.md)).
Avoid: "user data", "profile" (too vague).

### Account merge
Folding all of one user's account composition into another, then soft-deleting the
source. Triggered during an OAuth binding flow when a provider or email resolves to
a different existing user. Migrates every relation type and revokes the source's
sessions (see [ADR-003](docs/adr-003-oauth-user-merge.md), [ADR-010](docs/adr-010-account-composition-service.md)).
Avoid: "account linking" (that's adding a provider, not merging two users).

### Auth provider
A login method attached to a user: `Password`, `Github`, or `Google`. A user may
have several. The `Password` provider is paired with a `PasswordCredential` and is
excluded from the public provider list. Unlinking the last login method is rejected.
Avoid: "identity provider", "social login" when you mean the stored record.

### Session
A server-side login instance for a user, identified by a `sid` claim in the access
token. Owns one or more refresh tokens and carries device/IP metadata. Revoking a
session revokes its refresh tokens (see [ADR-001](docs/adr-001-session-plus-jwt.md)).
Avoid: "login", "token" (a session is neither).

### Refresh token
A long-lived credential bound to a session, exchanged for a new access token. Each
use rotates it: the old token is revoked and a new one issued (see
[ADR-007](docs/adr-007-refresh-token-rotation.md)). Distinct from the short-lived
access token (a signed JWT, never stored server-side). Not to be confused with
the *OIDC refresh token* an OIDC client receives from `/connect/token` — that
one belongs to a grant, not a session, and dying sessions don't kill it.

### Grant
A user's standing delegation to an OIDC client: the OpenIddict authorization
plus its tokens, keyed on `sub`. Owns the OIDC refresh tokens issued to that
client. Its lifecycle is deliberately independent of any session — logging out
of AuthService does not log the user out of downstream clients (the Google
model). It dies only when revoked explicitly or when the user is soft-deleted/
merged (decided 2026-07-09). Avoid: "connection", "authorization" (ambiguous
with the endpoint).

### Binding flow
An interactive browser flow that attaches a new auth provider to the current
account: a top-level **POST** to `/connect/bind/{provider}`, authenticated by
the interactive cookie (with the same DB session-liveness check as authorize),
then the provider round-trip with `currentUserId` carried in the protected
OAuth state (see [ADR-019](docs/adr-019-bind-as-interactive-flow.md)). Not a
login: completing a bind never mints a new session. It may trigger an account
merge if the provider or email already belongs to another user.
Avoid: "connect", "link account" as the formal term.

### Password reset
The unauthenticated forgot-password flow: proof is control of a **verified**
email address. A single-use, 30-minute, hash-stored token is emailed; consuming
it sets the password and revokes **all** sessions (see
[ADR-015](docs/adr-015-password-reset-and-change.md)). For an OAuth-only user
it sets a first password. Avoid: "password recovery", and don't conflate with
password change.

### Password change
The authenticated flow: proof is the **current password**, not the session —
a stolen session must not rotate the credential. Revokes every session except
the caller's own (see [ADR-015](docs/adr-015-password-reset-and-change.md)).
Distinct from password *set* (OAuth-only user adding a first password from the
dashboard, session is proof enough).

### Soft delete
Users are never hard-deleted; `IsDeleted = true` marks them gone (see
[ADR-006](docs/adr-006-soft-delete.md)). The invariant is enforced by cascade
EF global query filters — a soft-deleted user and every row it owns are
invisible to all queries (see [ADR-014](docs/adr-014-cascade-soft-delete-filters.md)).
A soft-deleted user is the result of an account merge. Global-uniqueness
checks (e.g. username) must use `IgnoreQueryFilters()`, since the unique
indexes still see deleted rows.

### OIDC client
A third-party application (e.g. OpenList) registered with AuthService as its
identity provider. Clients live in the OpenIddict tables and are managed
through the admin UI — the database is the sole source of truth (see
[ADR-016](docs/adr-016-openiddict-oidc-provider.md),
[ADR-017](docs/adr-017-admin-role-and-ui-managed-oidc-clients.md)).
Distinct from an *auth provider*, which is an upstream login method (GitHub/
Google) this service consumes. Avoid: "app", "relying party" in code.

### Interactive cookie
The `Interactive`-scheme cookie (`authservice.sso`, `Path=/connect`) issued
alongside tokens when a login completes in the browser. It only identifies the
browser to the interactive endpoints under `/connect` — `/connect/authorize`
and `/connect/bind/{provider}`; the session row in the database remains the
authority — those endpoints re-check `sid` revocation/expiry on every request
(see [ADR-016](docs/adr-016-openiddict-oidc-provider.md),
[ADR-019](docs/adr-019-bind-as-interactive-flow.md)).
Avoid: "SSO session" (it is not a session, just a pointer to one).

### Role
Internal authorization tier on `User` (`User` / `Admin`) that governs only
AuthService's own admin surface (OIDC client management, future admin panel).
It is deliberately **never emitted** in any token, OIDC claim, or userinfo
response: admin endpoints consult the user record directly, so grants and
revocations take effect immediately and downstream services physically cannot
build authorization on it. Downstream permission systems (e.g. a membership
tier in a download site) live in the downstream service's own data, keyed on
`sub`. The bootstrap admin is designated by deployment configuration.
Avoid: "permission", "scope" (OIDC scopes are unrelated to Role).


