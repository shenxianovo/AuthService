# ADR-008: Result Pattern over Exception-Driven Error Handling

## Status: Accepted

## Date: 2026-04-13

[`1dc367c`](https://github.com/shenxianovo/AuthService/commit/1dc367c) — refactor: introduce Result\<T\> pattern for controller-facing service interfaces
[`9da299c`](https://github.com/shenxianovo/AuthService/commit/9da299c) — refactor: complete Result unification for all service interfaces

## Context

Business logic errors (e.g. "email already exists", "invalid credentials") need to be communicated from services to controllers. Two approaches:

1. **Throw exceptions**: Service throws `EmailAlreadyExistsException`, controller catches it
2. **Return result**: Service returns `Result<T>` with either a value or a typed error code

## Decision

Use **discriminated union Result pattern**:

```csharp
Result<AuthResponse> result = await service.RegisterAsync(request, ip, agent);

if (result.IsSuccess)
    return Ok(result.Value);
else
    return result.ToErrorResponse();  // maps AuthError → HTTP status
```

Typed error codes (`AuthError` enum) are mapped to HTTP responses via extension method.

## Consequences

- ✅ No exception-driven control flow (exceptions are for unexpected failures only)
- ✅ Exhaustive error handling — compiler helps ensure all cases are covered
- ✅ Clean service → controller contract: return type tells the full story
- ✅ Easy to test: assert on `result.IsSuccess` / `result.Error`
- ⚠️ Slightly more boilerplate than throwing exceptions

## References

- [`Result.cs`](../backend/AuthService/Common/Result.cs) — `Result<T>`, `AuthError` enum
- [`ControllerBaseExtensions.cs`](../backend/AuthService/Extensions/ControllerBaseExtensions.cs) — `ToErrorResponse` mapping
