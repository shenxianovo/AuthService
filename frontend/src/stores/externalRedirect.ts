import { ref } from 'vue'

// Mirrors the backend's OAuthSecurity:AllowedRedirectOrigins semantics
// (OAuthSecurityService.ValidateRedirectUrl): exact origin match, or a
// "*." wildcard matching the base domain and any subdomain. Tokens are
// appended to this URL after login, so nothing outside this list may
// ever be honored.
const ALLOWED_REDIRECT_ORIGINS = ['https://*.shenxianovo.com']

const externalRedirect = ref<string | null>(null)

function isAllowedRedirect(raw: string): boolean {
  let url: URL
  try {
    url = new URL(raw)
  } catch {
    return false
  }
  if (url.protocol !== 'https:' && url.protocol !== 'http:') return false

  const scheme = url.protocol.slice(0, -1).toLowerCase()
  const host = url.hostname.toLowerCase()
  const origin = url.origin.toLowerCase()

  return ALLOWED_REDIRECT_ORIGINS.some((allowed) => {
    const trimmed = allowed.replace(/\/+$/, '').toLowerCase()
    if (trimmed.includes('*.')) {
      const [allowedScheme, allowedHost] = trimmed.split('://')
      if (scheme !== allowedScheme) return false
      const baseDomain = allowedHost.replace('*.', '')
      return host === baseDomain || host.endsWith(`.${baseDomain}`)
    }
    return origin === trimmed
  })
}

export function useExternalRedirect() {
  function init() {
    const params = new URLSearchParams(window.location.search)
    const redirect = params.get('redirect')
    if (redirect && isAllowedRedirect(redirect)) {
      externalRedirect.value = redirect
      sessionStorage.setItem('externalRedirect', redirect)
    } else {
      const stored = sessionStorage.getItem('externalRedirect')
      if (stored && isAllowedRedirect(stored)) externalRedirect.value = stored
    }
  }

  function clear() {
    externalRedirect.value = null
    sessionStorage.removeItem('externalRedirect')
  }

  return { externalRedirect, init, clear }
}
