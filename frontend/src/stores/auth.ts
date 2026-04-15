import { reactive, readonly, computed, ref } from 'vue'

export type AppState = 'login' | 'register' | 'email-verify' | 'profile'

export interface AuthTokens {
  accessToken: string
  refreshToken: string
  expiresAt: Date
  userId: string
}

interface AuthState {
  tokens: AuthTokens | null
}

const state = reactive<AuthState>({
  tokens: _loadFromStorage(),
})

function _loadFromStorage(): AuthTokens | null {
  const accessToken = localStorage.getItem('access_token')
  const refreshToken = localStorage.getItem('refresh_token')
  const expiresAtStr = localStorage.getItem('token_expires_at')
  const userId = localStorage.getItem('user_id')

  if (accessToken && refreshToken && expiresAtStr && userId) {
    const expiresAt = new Date(expiresAtStr)
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

const appState = ref<AppState>(state.tokens !== null ? 'profile' : 'login')

export const authStore = {
  state: readonly(state),

  appState,

  isAuthenticated: computed(() => state.tokens !== null),

  transition(to: AppState) {
    appState.value = to
  },

  setTokens(accessToken: string, refreshToken: string, expiresAt: Date, userId: string) {
    const tokens: AuthTokens = { accessToken, refreshToken, expiresAt, userId }
    state.tokens = tokens
    _saveToStorage(tokens)
  },

  clearTokens() {
    state.tokens = null
    _clearStorage()
  },
}