<template>
  <div>
    <div class="tabs">
      <button class="active">Sign in</button>
      <button @click="$router.push('/register')">Sign up</button>
    </div>

    <div class="oauth-buttons">
      <button class="btn btn-github" @click="handleGithubLogin" :disabled="loading">
        <svg class="icon" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>
        Continue with GitHub
      </button>
      <button class="btn btn-google" @click="handleGoogleLogin" :disabled="loading">
        <svg class="icon" viewBox="0 0 24 24"><path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/><path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/><path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/><path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/></svg>
        Continue with Google
      </button>
    </div>

    <div class="oauth-divider"><span>or</span></div>

    <LoginForm
      v-model:email="form.email"
      v-model:password="form.password"
      :loading="loading"
      @submit="handleLogin"
    />

    <div class="forgot-link">
      <router-link to="/forgot-password">Forgot password?</router-link>
    </div>

    <div v-if="error" class="message error">{{ error }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import LoginForm from '@/components/LoginForm.vue'
import { authStore } from '@/stores/auth'
import { userStore } from '@/stores/user'
import { useExternalRedirect } from '@/stores/externalRedirect'
import * as api from '@/api'
import type { AuthResponse } from '@/api'

const router = useRouter()
const { externalRedirect, clear: clearRedirect } = useExternalRedirect()

const form = ref({ email: '', password: '' })
const loading = ref(false)
const error = ref<string | null>(null)

function applyAuthResponse(data: AuthResponse) {
  authStore.setTokens(
    data.accessToken!,
    data.refreshToken!,
    data.expiresAt instanceof Date ? data.expiresAt : new Date(data.expiresAt as unknown as string),
    data.userId!.toString(),
  )
}

function redirectToExternal(data: AuthResponse) {
  const url = new URL(externalRedirect.value!)
  url.searchParams.set('token', data.accessToken!)
  url.searchParams.set('userId', data.userId!.toString())
  if (data.refreshToken) url.searchParams.set('refreshToken', data.refreshToken)
  clearRedirect()
  window.location.href = url.toString()
}

async function handleLogin() {
  error.value = null
  loading.value = true
  try {
    const data = await api.login(form.value.email, form.value.password)
    applyAuthResponse(data)
    if (externalRedirect.value) { redirectToExternal(data); return }
    await userStore.fetch()
    router.push('/dashboard/profile')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Login failed'
  } finally {
    loading.value = false
  }
}

const currentPageUrl = () => window.location.origin + '/callback'

function handleGithubLogin() {
  if (externalRedirect.value) sessionStorage.setItem('externalRedirect', externalRedirect.value)
  window.location.href = api.githubLoginUrl(currentPageUrl())
}

function handleGoogleLogin() {
  if (externalRedirect.value) sessionStorage.setItem('externalRedirect', externalRedirect.value)
  window.location.href = api.googleLoginUrl(currentPageUrl())
}
</script>

<style scoped>
.tabs { display: flex; margin-bottom: 24px; background: #f5f5f7; border-radius: 10px; padding: 3px; }
.tabs button { flex: 1; padding: 10px; border: none; background: transparent; border-radius: 8px; font-size: 14px; font-weight: 500; color: #888; cursor: pointer; transition: all .2s; }
.tabs button.active { background: #fff; color: #1a1a2e; box-shadow: 0 1px 3px rgba(0,0,0,.1); font-weight: 600; }
.oauth-buttons { display: flex; flex-direction: column; gap: 10px; }
.oauth-divider { display: flex; align-items: center; margin: 20px 0; color: #aaa; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; }
.oauth-divider::before, .oauth-divider::after { content: ""; flex: 1; height: 1px; background: #e8e8e8; }
.oauth-divider span { padding: 0 14px; }
.icon { width: 18px; height: 18px; flex-shrink: 0; }
.message { margin-top: 16px; padding: 10px 14px; border-radius: 8px; font-size: 13px; text-align: center; }
.message.error { background: #fff0f0; color: #dc3545; border: 1px solid #ffcdd2; }
.forgot-link { margin-top: 12px; text-align: right; font-size: 13px; }
.forgot-link a { color: #555; text-decoration: none; }
.forgot-link a:hover { text-decoration: underline; }
</style>
