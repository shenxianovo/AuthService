/**
 * API layer — wraps NSwag-generated clients with auth token injection and 401 auto-refresh.
 *
 * All public functions are thin wrappers around the generated client classes.
 * Types are re-exported from client.ts for convenience.
 */

import { authStore } from '@/stores/auth'
import {
  ExchangeClient,
  PasswordAuthClient,
  SessionClient,
  UserClient,
  ApiKeyClient,
  VerifyEmailRequest,
  AddEmailRequest,
  CreateApiKeyRequest,
} from './client'

export type {
  AuthResponse,
  UserInfoResponse,
  EmailInfo,
  ProviderInfo,
  ProblemDetails,
  CreateApiKeyResponse,
  ApiKeyListItem,
} from './client'

export { ApiException } from './client'

import type { AuthResponse } from './client'

// ---------- constants ----------

const API_BASE = '/api/v1/auth'

/** Base URL passed to NSwag clients — empty string so they use relative paths through Vite proxy. */
const CLIENT_BASE = ''

// ---------- auth-aware http adapter ----------

/** Singleton refresh guard — prevents parallel refresh storms. */
let _refreshing: Promise<boolean> | null = null

async function tryRefresh(): Promise<boolean> {
  if (_refreshing) return _refreshing

  const refreshToken = authStore.state.tokens?.refreshToken
  if (!refreshToken) return false

  _refreshing = (async () => {
    try {
      // Use a raw SessionClient (without auth http) since refresh doesn't need Authorization header
      const raw = new SessionClient(CLIENT_BASE)
      const data = await raw.refresh({ refreshToken } as any)
      authStore.setTokens(
        data.accessToken!,
        data.refreshToken!,
        data.expiresAt instanceof Date ? data.expiresAt : new Date(data.expiresAt as unknown as string),
        authStore.state.tokens?.userId ?? '',
      )
      return true
    } catch {
      return false
    } finally {
      _refreshing = null
    }
  })()

  return _refreshing
}

/**
 * Custom http adapter injected into NSwag clients.
 * - Adds Authorization header
 * - On 401: tries one silent token refresh, then replays the request
 */
const authHttp = {
  async fetch(url: RequestInfo, init?: RequestInit): Promise<Response> {
    // Inject token
    const token = authStore.state.tokens?.accessToken
    if (token) {
      const headers = new Headers(init?.headers)
      headers.set('Authorization', `Bearer ${token}`)
      init = { ...init, headers }
    }

    const response = await fetch(url, init)

    if (response.status !== 401) return response

    // Try refresh
    const refreshed = await tryRefresh()
    if (!refreshed) {
      authStore.clearTokens()
      return response
    }

    // Replay with new token
    const newToken = authStore.state.tokens?.accessToken
    if (newToken) {
      const headers = new Headers(init?.headers)
      headers.set('Authorization', `Bearer ${newToken}`)
      init = { ...init, headers }
    }

    const retryResponse = await fetch(url, init)
    if (retryResponse.status === 401) {
      authStore.clearTokens()
    }
    return retryResponse
  },
}

// ---------- client instances ----------

const exchangeClient = new ExchangeClient(CLIENT_BASE)
const passwordClient = new PasswordAuthClient(CLIENT_BASE)
const authPasswordClient = new PasswordAuthClient(CLIENT_BASE, authHttp)
const sessionClient = new SessionClient(CLIENT_BASE, authHttp)
const userClient = new UserClient(CLIENT_BASE, authHttp)
const apiKeyClient = new ApiKeyClient(CLIENT_BASE, authHttp)

// ---------- public API ----------

export async function register(displayName: string, email: string, password: string): Promise<AuthResponse> {
  return passwordClient.register({ displayName, email, password } as any)
}

export async function sendVerificationCode(email?: string): Promise<void> {
  await authPasswordClient.sendVerificationCode(email)
}

export async function verifyEmail(code: string, email?: string): Promise<void> {
  await authPasswordClient.verifyEmail(email, new VerifyEmailRequest({ code }))
}

export async function login(email: string, password: string): Promise<AuthResponse> {
  return passwordClient.login({ email, password } as any)
}

export async function exchangeCode(code: string): Promise<AuthResponse> {
  return exchangeClient.exchangeCode({ code } as any)
}

export async function logout(): Promise<void> {
  try {
    await sessionClient.logout()
  } catch { /* ignore */ }
}

export async function fetchMe() {
  return userClient.getMe()
}

export async function addPassword(password: string): Promise<void> {
  await userClient.addPassword({ password } as any)
}

// ---------- OAuth redirect URLs ----------

export function githubLoginUrl(redirectUrl: string): string {
  return `${API_BASE}/github/login?redirectUrl=${encodeURIComponent(redirectUrl)}`
}

export function googleLoginUrl(redirectUrl: string): string {
  return `${API_BASE}/google/login?redirectUrl=${encodeURIComponent(redirectUrl)}`
}

export function githubBindUrl(redirectUrl: string, accessToken: string): string {
  return `${API_BASE}/github/login?redirectUrl=${encodeURIComponent(redirectUrl)}&token=${encodeURIComponent(accessToken)}`
}

export function googleBindUrl(redirectUrl: string, accessToken: string): string {
  return `${API_BASE}/google/login?redirectUrl=${encodeURIComponent(redirectUrl)}&token=${encodeURIComponent(accessToken)}`
}

export async function unlinkProvider(provider: string): Promise<void> {
  await userClient.unlinkProvider({ provider } as any)
}

export async function addEmail(email: string): Promise<void> {
  await authPasswordClient.addEmail(new AddEmailRequest({ email }))
}

export async function removeEmail(email: string): Promise<void> {
  await authPasswordClient.removeEmail(email)
}

export async function setPrimaryEmail(email: string): Promise<void> {
  await authPasswordClient.setPrimaryEmail(email)
}

// ---------- API Keys ----------

export async function createApiKey(name: string) {
  return apiKeyClient.create(new CreateApiKeyRequest({ name }))
}

export async function listApiKeys() {
  return apiKeyClient.list()
}

export async function revokeApiKey(id: string) {
  await apiKeyClient.revoke(id)
}
