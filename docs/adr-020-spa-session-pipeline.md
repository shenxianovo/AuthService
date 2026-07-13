# ADR-020: First-Party SPA Rides the Session Pipeline

## Status: Accepted

## Date: 2026-07-13

## Context

With OpenIddict serving both federation directions — upstream providers in
([ADR-018](adr-018-openiddict-client-oauth.md)), downstream RPs out
([ADR-016](adr-016-openiddict-oidc-provider.md)) — the obvious next question
is whether the first-party SPA should become an OIDC public client of its own
service (code + PKCE), retiring the hand-rolled token issuance
([ADR-001](adr-001-session-plus-jwt.md)), refresh rotation
([ADR-007](adr-007-refresh-token-rotation.md)) and the one-time auth-code
handoff ([ADR-004](adr-004-auth-code-exchange.md)) in favor of a single
issuance pipeline. This was floated as a "clean end state" during the
2026-07-13 design review. On closer analysis the end state is structurally
unattainable, and pursuing it would damage semantics that were just settled.

## Decision

The SPA stays on the session pipeline. It is **not** an OIDC client of its
own IdP, now or as a direction. The governing mental model:

> **OpenIddict at the edges, sessions at the core.** Every protocol
> conversation with *another party* — upstream providers in, downstream RPs
> out — goes through OpenIddict and is never hand-written. The session
> pipeline is not legacy debt: it is the IdP's own login state, which every
> real IdP (Google, Keycloak, Auth0) also implements outside the OIDC
> standard. The first-party SPA is the IdP's own face and rides it directly.

The structural reasons:

1. **The SPA is the login surface.** It is where `/connect/authorize`
   challenges to when no interactive cookie is present; it must call the
   password endpoints directly and establish the interactive cookie. That
   role cannot be an OIDC client of itself — some component always sits
   beneath the protocol, and this is it. At most the account-console half
   could migrate, so "one pipeline" was never on the table.
2. **Grant semantics would need an immediate exception.** Grants are
   deliberately session-independent — logout does not end downstream logins
   (CONTEXT.md *Grant*, decided 2026-07-09). A first-party SPA client would
   require the opposite: its grant must die on logout. Migrating would trade
   one extra pipeline for a hole in a just-established rule.
3. **The payoff is small and already priced in.** Migration would delete the
   one-time auth-code handoff and browser refresh rotation, while adding an
   OIDC client library and an authorize round-trip to the SPA. Adding new
   upstream providers — the original motive for OpenIddict — is already one
   registration line regardless (ADR-018).
4. **Token-species separation is verified, not assumed.** Session JWTs carry
   a fixed `iss`/`aud` enforced at validation (`JwtService`); OpenIddict
   access tokens carry `typ: at+jwt` and resource-based audiences. The two
   pipelines share one RSA key without cross-acceptance.

## Consequences

- ✅ One sentence answers "why isn't the SPA an OIDC client of itself?" for
  every future reader and agent; the question stops being re-litigated.
- ✅ The three credential planes (session / grant / API key, see CONTEXT.md)
  each keep a single lifecycle authority with no first-party exception.
- ✅ Hand-rolled session code has a defined charter: first-party browser
  login state only. Anything protocol-facing that grows beyond that charter
  belongs in OpenIddict.
- ⚠️ Two token-issuance code paths remain permanently. Accepted: they serve
  different planes, and their non-interference is pinned by `aud`/`typ`.
- ⚠️ Supersedes the "long-term option: SPA as first-party OIDC client"
  direction discussed (and initially favored) in the 2026-07-13 review.

## References

- [ADR-001](adr-001-session-plus-jwt.md), [ADR-004](adr-004-auth-code-exchange.md),
  [ADR-007](adr-007-refresh-token-rotation.md) — the session pipeline this confirms
- [ADR-016](adr-016-openiddict-oidc-provider.md), [ADR-018](adr-018-openiddict-client-oauth.md) — the OpenIddict edges
- [ADR-019](adr-019-bind-as-interactive-flow.md) — interactive surface the SPA fronts
- [`JwtService.cs`](../backend/AuthService/Services/JwtService.cs) — fixed-audience validation
- `CONTEXT.md` — *Session*, *Grant*, *Interactive cookie* entries
