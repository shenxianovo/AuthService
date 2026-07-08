import { ref } from 'vue'

const returnUrl = ref<string | null>(null)

/**
 * Only same-origin OIDC authorize paths are honored — anything else
 * (absolute URLs, protocol-relative, other paths) is ignored, so this
 * can never become an open redirect.
 */
function isSafeReturnUrl(url: string): boolean {
  return url.startsWith('/connect/authorize')
}

/**
 * When a third-party app (e.g. OpenList) starts an OIDC flow and the user is
 * not signed in, the backend redirects to /login?returnUrl=/connect/authorize?...
 * We stash that here (sessionStorage survives the GitHub/Google full-page
 * round trip) and navigate back to it after login completes — a full-page
 * navigation so the freshly issued SSO cookie rides along.
 */
export function useOidcReturnUrl() {
  function init() {
    const params = new URLSearchParams(window.location.search)
    const fromQuery = params.get('returnUrl')
    if (fromQuery && isSafeReturnUrl(fromQuery)) {
      returnUrl.value = fromQuery
      sessionStorage.setItem('oidcReturnUrl', fromQuery)
    } else {
      const stored = sessionStorage.getItem('oidcReturnUrl')
      if (stored && isSafeReturnUrl(stored)) returnUrl.value = stored
    }
  }

  /** Return the pending URL (or null) and clear it so it can't fire twice. */
  function consume(): string | null {
    const url = returnUrl.value
    returnUrl.value = null
    sessionStorage.removeItem('oidcReturnUrl')
    return url
  }

  return { returnUrl, init, consume }
}
