# Honor prompt=login (forced re-authentication) on /connect/authorize

Status: needs-triage

## Context

Discovery advertises `prompt_values_supported`, but `AuthorizationController`
ignores `prompt=login` — an RP requesting forced re-auth silently gets SSO.
No current client sends it (OpenList doesn't). Implement when a client needs
it: on `prompt=login`, sign out the interactive cookie and challenge, and
strip the prompt from the returnUrl to avoid a loop.
