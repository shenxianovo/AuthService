import { reactive, readonly, computed } from 'vue'
import type { TokenStore } from '@/api/AuthClientBase'

export interface AuthTokens {
  accessToken: string
  refreshToken: string
  expiresAt: Date
  userId: string
}

interface AuthState {
  tokens: AuthTokens | null
  isLoggingOut: boolean
}

const state = reactive<AuthState>({
  tokens: _loadFromStorage(),
  isLoggingOut: false,
})

function _loadFromStorage(): AuthTokens | null {
  const accessToken = localStorage.getItem('access_token')
  const refreshToken = localStorage.getItem('refresh_token')
  const expiresAtStr = localStorage.getItem('token_expires_at')
  const userId = localStorage.getItem('user_id')

  if (accessToken && refreshToken && expiresAtStr && userId) {
    const expiresAt = new Date(expiresAtStr)
    // Don't restore if already expired
    if (expiresAt > new Date()) {
      return { accessToken, refreshToken, expiresAt, userId }
    }
  }
  return null
}

function _saveToStorage(tokens: AuthTokens) {
  localStorage.setItem('access_token', tokens.accessToken)
  localStorage.setItem('refresh_token', tokens.refreshToken)
  localStorage.setItem('token_expires_at', tokens.expiresAt.toISOString())
  localStorage.setItem('user_id', tokens.userId)
}

function _clearStorage() {
  localStorage.removeItem('access_token')
  localStorage.removeItem('refresh_token')
  localStorage.removeItem('token_expires_at')
  localStorage.removeItem('user_id')
}

export const authStore = {
  state: readonly(state),

  isAuthenticated: computed(() => state.tokens !== null),

  setTokens(accessToken: string, refreshToken: string, expiresAt: Date, userId: string) {
    const tokens: AuthTokens = { accessToken, refreshToken, expiresAt, userId }
    state.tokens = tokens
    _saveToStorage(tokens)
  },

  clearTokens() {
    state.tokens = null
    _clearStorage()
  },

  /** TokenStore interface for AuthClientBase */
  asTokenStore(): TokenStore {
    return {
      getAccessToken: () => state.tokens?.accessToken ?? null,
      getRefreshToken: () => state.tokens?.refreshToken ?? null,
      setTokens: (accessToken, refreshToken, expiresAt) => {
        const userId = state.tokens?.userId ?? ''
        authStore.setTokens(accessToken, refreshToken, expiresAt, userId)
      },
      clearTokens: () => authStore.clearTokens(),
    }
  },
}
