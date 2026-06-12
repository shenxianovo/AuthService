# Context

Domain glossary for AuthService — the authoritative identity/auth service. Other
services (e.g. Heartbeat) depend on the terms defined here. When naming a concept
in code, tests, issues, or ADRs, use the term as defined below; avoid the listed
synonyms.

## Glossary

### Account composition
The full set of data that constitutes a user: the `User` row plus its emails,
auth providers, password credential, sessions, and API keys. `AccountService` is
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
access token (a signed JWT, never stored server-side).

### Binding flow
An OAuth callback made while the user is already authenticated (a `currentUserId`
is present, carried in the signed OAuth state). The intent is to attach a new
provider to the current account rather than log in — and it may trigger an account
merge if the provider or email already belongs to another user.
Avoid: "connect", "link account" as the formal term.

### Soft delete
Users are never hard-deleted; `IsDeleted = true` marks them gone (see
[ADR-006](docs/adr-006-soft-delete.md)). The invariant is enforced by cascade
EF global query filters — a soft-deleted user and every row it owns are
invisible to all queries (see [ADR-014](docs/adr-014-cascade-soft-delete-filters.md)).
A soft-deleted user is the result of an account merge. Global-uniqueness
checks (e.g. username) must use `IgnoreQueryFilters()`, since the unique
indexes still see deleted rows.

