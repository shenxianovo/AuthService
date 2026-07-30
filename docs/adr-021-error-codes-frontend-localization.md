# ADR-021: Error Responses Carry a Stable Code; Localization Lives in the Frontend

## Status: Accepted

## Date: 2026-07-30

## Context

The SPA is gaining Chinese localization (vue-i18n, `en` + `zh-CN`). UI strings
are the frontend's own to translate, but error messages the user sees mostly
originate in the backend: every business failure resolves through the central
`AuthError` → HTTP status/message map (`AuthErrorHttp`,
[ADR-008](adr-008-result-pattern.md)), and the SPA displayed the response's
`message` string verbatim. Two ways to localize those:

1. **Backend translates** — `Accept-Language` + `IStringLocalizer`/.resx;
   the SPA keeps displaying `message` as-is.
2. **Frontend translates** — the error response exposes the `AuthError` code
   as a stable, machine-readable field; the SPA maps codes to localized text
   and keeps the English `message` as a fallback.

A separate long-standing gap made the choice easier: error responses were an
anonymous `{ message }` — string matching was the only way a consumer (the
SPA, Heartbeat, any RP) could distinguish failure kinds. An error *code* in
the contract is API hygiene independent of localization.

## Decision

Option 2. `ToErrorResponse` returns a typed `ErrorResponse { code, message }`
DTO, where `code` is the `AuthError` enum name serialized as a string
(`JsonStringEnumConverter` on the enum) and `message` stays the English
default from `AuthErrorHttp`. An assembly-level `ProducesErrorResponseType`
points untyped 4xx `[ProducesResponseType]` declarations at the DTO, so NSwag
emits `AuthError` and `ErrorResponse` into the generated TS client and throws
`ErrorResponse` instances on non-2xx.

The frontend translation table for errors is keyed by `AuthError` enum name
and typed `Record<TranslatedAuthError, string>` — a new backend error code
propagates through client regeneration into a `vue-tsc` compile error until a
translation (or an explicit admin-only exclusion) is added. This closes the
same drift loop that `AuthErrorHttpMappingTests` closes for the
status/message map. Codes without a translation entry (admin-only OIDC
management errors — that page deliberately stays English) fall back to the
backend's English `message`.

The backend remains locale-unaware. Transactional email localization is out
of scope here and will be decided separately (it needs a per-user language
preference, not a per-request header).

## Consequences

- ✅ `code` is now part of the API contract — downstream services can branch
  on failure kind without string matching; enum names must stay stable.
- ✅ One translation infrastructure (vue-i18n), not two (.resx + vue-i18n);
  all user-facing text is versioned and reviewed in one place.
- ✅ Drift-proof both directions: enum ↔ HTTP map guarded by tests, enum ↔
  translations guarded by the type system.
- ✅ Purely additive and backward compatible — `message` is unchanged, so
  existing consumers keep working.
- ⚠️ Renaming an `AuthError` member is now a breaking API change, not a
  refactor.
- ⚠️ Non-SPA consumers that show `message` to end users get English only; if
  a downstream ever needs server-side localization this decision must be
  revisited.

## References

- [`ErrorResponse.cs`](../backend/AuthService/DTOs/ErrorResponse.cs) — the typed error body
- [`ControllerBaseExtensions.cs`](../backend/AuthService/Extensions/ControllerBaseExtensions.cs) — `ToErrorResponse` + `AuthErrorHttp` map
- [`Result.cs`](../backend/AuthService/Common/Result.cs) — `AuthError` enum (string-serialized)
- [`frontend/src/i18n/`](../frontend/src/i18n/) — vue-i18n setup, `en`/`zh-CN` packs, `translateApiError`
