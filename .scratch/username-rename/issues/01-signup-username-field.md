# Signup form missing username field — email registration broken

Status: open

## Context

Backend `POST /api/v1/auth/register` requires `Username` (3-39 chars,
validated + lowercased in `PasswordAuthService.cs:31-50`), but the frontend
never sends it:

```typescript
// frontend/src/api/index.ts:137-138
export async function register(displayName: string, email: string, password: string) {
  return passwordClient.register({ displayName, email, password } as any)  // no username
}
```

`RegisterForm.vue` has Display name / Email / Password only. The `as any`
cast suppresses the TypeScript error that would have caught this. Email
registration most likely 400s (or NREs on `request.Username.ToLowerInvariant()`)
— unnoticed because all real signups went through OAuth.

Decided 2026-07-17 (grilling session, Heartbeat ADR-027 context): keep
username as an explicit user choice at signup — add the field, do NOT
auto-generate from email on this path (auto-generation stays OAuth-only).

## Acceptance

- `RegisterForm.vue`: username input (client-side hint: 3-39 chars, lowercase
  letters/digits/hyphens); no live availability check — submit-time backend
  error is enough at this scale.
- `api/index.ts` `register()`: pass `username` through, delete the `as any`.
- Manual test: email registration succeeds end-to-end; duplicate username
  shows the backend error.
