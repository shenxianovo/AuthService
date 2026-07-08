# End-session endpoint (/connect/logout, single logout)

Status: needs-triage

## Context

No RP-initiated logout: signing out of a downstream doesn't end the
AuthService SSO cookie. OpenList has no single-logout support, so there is no
consumer today. When needed: `SetEndSessionEndpointUris("connect/logout")`,
validate `post_logout_redirect_uri` against the client registration, SignOut
of both the interactive cookie and the OpenIddict scheme.
