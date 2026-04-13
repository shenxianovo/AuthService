/**
 * AuthClientBase — base class injected into the NSwag-generated AuthClient.
 *
 * Responsibilities:
 *  - Attach Authorization: Bearer <token> header on every request (via transformOptions)
 *  - On 401: attempt one silent token refresh, then replay the request (via transformResult)
 *  - On second 401: clear tokens and fire onUnauthorized callback
 */

export type TokenStore = {
  getAccessToken: () => string | null
  getRefreshToken: () => string | null
  setTokens: (accessToken: string, refreshToken: string, expiresAt: Date) => void
  clearTokens: () => void
}

let _store: TokenStore = {
  getAccessToken: () => localStorage.getItem('access_token'),
  getRefreshToken: () => localStorage.getItem('refresh_token'),
  setTokens: (a, r, e) => {
    localStorage.setItem('access_token', a)
    localStorage.setItem('refresh_token', r)
    localStorage.setItem('token_expires_at', e.toISOString())
  },
  clearTokens: () => {
    localStorage.removeItem('access_token')
    localStorage.removeItem('refresh_token')
    localStorage.removeItem('token_expires_at')
  },
}

let _onUnauthorized: (() => void) | null = null

/** Call once in main.ts to wire up the token store and logout callback. */
export function configureAuthClient(store: TokenStore, onUnauthorized: () => void): void {
  _store = store
  _onUnauthorized = onUnauthorized
}

/** Singleton refresh promise — prevents parallel refresh storms. */
let _refreshing: Promise<boolean> | null = null

async function tryRefresh(): Promise<boolean> {
  if (_refreshing) return _refreshing

  const refreshToken = _store.getRefreshToken()
  if (!refreshToken) return false

  _refreshing = (async () => {
    try {
      const res = await fetch('/api/v1/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!res.ok) return false
      const data: { accessToken: string; refreshToken: string; expiresAt: string } = await res.json()
      _store.setTokens(data.accessToken, data.refreshToken, new Date(data.expiresAt))
      return true
    } catch {
      return false
    } finally {
      _refreshing = null
    }
  })()

  return _refreshing
}

export class AuthClientBase {
  /** NSwag calls this before every fetch — inject Bearer header. */
  protected async transformOptions(options: RequestInit): Promise<RequestInit> {
    const token = _store.getAccessToken()
    if (token) {
      options.headers = {
        ...options.headers,
        Authorization: `Bearer ${token}`,
      }
    }
    return options
  }

  /**
   * NSwag calls this after every fetch.
   * On 401: try one silent refresh + re-fetch with new token.
   * On second 401 or failed refresh: clear tokens and call onUnauthorized.
   *
   * The generic parameter T matches the return type of the specific process* method,
   * so TypeScript is satisfied without needing casts in generated code.
   */
  protected async transformResult<T>(
    url: string,
    response: Response,
    processor: (response: Response) => Promise<T>,
  ): Promise<T> {
    if (response.status !== 401) {
      return processor(response)
    }

    const refreshed = await tryRefresh()
    if (!refreshed) {
      _store.clearTokens()
      _onUnauthorized?.()
      return processor(response) // let NSwag throw ApiException(401) as usual
    }

    // Replay the original request with the new access token.
    // Note: we only carry the new auth header; method/body are unknown here.
    // This works correctly for GET requests (e.g. /me).
    // POST requests that return 401 (e.g. login with wrong password) don't need retry.
    const retryResponse = await fetch(url, {
      headers: { Authorization: `Bearer ${_store.getAccessToken()}` },
    })

    if (retryResponse.status === 401) {
      _store.clearTokens()
      _onUnauthorized?.()
    }

    return processor(retryResponse)
  }
}