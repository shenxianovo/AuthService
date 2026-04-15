<template>
  <div class="auth-card">
    <div class="header">
      <h1>喵~</h1>
      <p v-if="externalRedirect" class="subtitle">
        Sign in to continue to <strong>{{ externalRedirectHost }}</strong>
      </p>
      <p v-else-if="appState !== 'profile'" class="subtitle">Sign in to your account</p>
    </div>

    <div class="auth-content">
      <!-- Profile -->
      <ProfileView
        v-if="appState === 'profile'"
        :userInfo="userInfo"
        :userId="authStore.state.tokens?.userId ?? ''"
        v-model:newPassword="addPasswordField"
        :loading="loading"
        @addPassword="handleAddPassword"
        @githubBind="handleGithubBind"
        @googleBind="handleGoogleBind"
        @unlinkProvider="handleUnlinkProvider"
        @verifyEmail="handleVerifyEmailFromProfile"
        @logout="handleLogout"
      />

      <!-- Email verification -->
      <EmailVerificationView
        v-else-if="appState === 'email-verify'"
        :email="pendingEmail"
        :loading="loading"
        :error="error ?? ''"
        @verify="handleVerifyEmail"
        @resend="handleResendCode"
      />

      <!-- Login / Register -->
      <template v-else>
        <div class="tabs">
          <button :class="{ active: appState === 'login' }" @click="authStore.transition('login')">Sign in</button>
          <button :class="{ active: appState === 'register' }" @click="authStore.transition('register')">Sign up</button>
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
        <RegisterForm
          v-if="appState === 'register'"
          v-model:displayName="registerForm.displayName"
          v-model:email="registerForm.email"
          v-model:password="registerForm.password"
          :loading="loading"
          @submit="handleRegister"
        />
        <LoginForm
          v-if="appState === 'login'"
          v-model:email="loginForm.email"
          v-model:password="loginForm.password"
          :loading="loading"
          @submit="handleLogin"
        />
      </template>

      <div v-if="error && appState !== 'email-verify'" class="message error">{{ error }}</div>
      <div v-if="successMsg" class="message success">{{ successMsg }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { authStore } from '@/stores/auth'
import * as api from '@/api'
import type { AuthResponse, UserInfoResponse } from '@/api'
import LoginForm from './LoginForm.vue'
import RegisterForm from './RegisterForm.vue'
import ProfileView from './ProfileView.vue'
import EmailVerificationView from './EmailVerificationView.vue'

const appState = authStore.appState

const loading = ref(false)
const error = ref<string | null>(null)
const successMsg = ref<string | null>(null)
const userInfo = ref<UserInfoResponse | null>(null)
const externalRedirect = ref<string | null>(null)
const registerForm = ref({ displayName: '', email: '', password: '' })
const loginForm = ref({ email: '', password: '' })
const addPasswordField = ref('')
const pendingEmail = ref('')

const externalRedirectHost = computed(() => {
  try { return externalRedirect.value ? new URL(externalRedirect.value).host : '' }
  catch { return externalRedirect.value ?? '' }
})

function resetMessages() { error.value = null; successMsg.value = null }

function applyAuthResponse(data: AuthResponse) {
  authStore.setTokens(
    data.accessToken!,
    data.refreshToken!,
    data.expiresAt instanceof Date ? data.expiresAt : new Date(data.expiresAt as unknown as string),
    data.userId!.toString(),
  )
}

async function fetchUserInfo() {
  if (!authStore.state.tokens) return
  try {
    userInfo.value = await api.fetchMe()
  } catch { /* session may be expired, api layer handles 401 */ }
}

async function handleRegister() {
  resetMessages(); loading.value = true
  try {
    const data = await api.register(
      registerForm.value.displayName,
      registerForm.value.email,
      registerForm.value.password,
    )
    applyAuthResponse(data)
    pendingEmail.value = registerForm.value.email
    await api.sendVerificationCode()
    authStore.transition('email-verify')
  } catch (e: unknown) { error.value = e instanceof Error ? e.message : 'Registration failed' }
  finally { loading.value = false }
}

async function handleVerifyEmail(code: string) {
  resetMessages(); loading.value = true
  try {
    await api.verifyEmail(code)
    await fetchUserInfo()
    authStore.transition('profile')
  } catch (e: unknown) { error.value = e instanceof Error ? e.message : 'Verification failed' }
  finally { loading.value = false }
}

async function handleResendCode() {
  resetMessages()
  try {
    await api.sendVerificationCode()
  } catch (e: unknown) { error.value = e instanceof Error ? e.message : 'Failed to resend code' }
}

async function handleVerifyEmailFromProfile() {
  resetMessages(); loading.value = true
  try {
    // pendingEmail from userInfo primary email
    pendingEmail.value = userInfo.value?.emails?.find(e => e.isPrimary)?.email ?? ''
    await api.sendVerificationCode()
    authStore.transition('email-verify')
  } catch (e: unknown) { error.value = e instanceof Error ? e.message : 'Failed to send verification code' }
  finally { loading.value = false }
}

async function handleLogin() {
  resetMessages(); loading.value = true
  try {
    const data = await api.login(loginForm.value.email, loginForm.value.password)
    applyAuthResponse(data)
    if (externalRedirect.value) { redirectToExternal(data); return }
    await fetchUserInfo()
    authStore.transition('profile')
  } catch (e: unknown) { error.value = e instanceof Error ? e.message : 'Login failed' }
  finally { loading.value = false }
}

async function handleLogout() {
  await api.logout()
  authStore.clearTokens(); userInfo.value = null; resetMessages()
  authStore.transition('login')
  window.history.replaceState({}, document.title, window.location.pathname)
}

async function handleAddPassword() {
  resetMessages(); loading.value = true
  try {
    await api.addPassword(addPasswordField.value)
    successMsg.value = 'Password successfully set!'
    addPasswordField.value = ''
    await fetchUserInfo()
  } catch (e: unknown) { error.value = e instanceof Error ? e.message : 'Failed to set password' }
  finally { loading.value = false }
}

const currentPageUrl = () => window.location.origin + window.location.pathname

const handleGithubLogin = () => {
  const r = externalRedirect.value ?? currentPageUrl()
  window.location.href = api.githubLoginUrl(r)
}
const handleGoogleLogin = () => {
  const r = externalRedirect.value ?? currentPageUrl()
  window.location.href = api.googleLoginUrl(r)
}
const linkedProviders = computed(() =>
  new Set((userInfo.value?.providers ?? []).map(p => p.provider?.toLowerCase()))
)

const handleGithubBind = () => {
  if (linkedProviders.value.has('github')) return
  const t = authStore.state.tokens?.accessToken ?? ''
  window.location.href = api.githubBindUrl(currentPageUrl(), t)
}
const handleGoogleBind = () => {
  if (linkedProviders.value.has('google')) return
  const t = authStore.state.tokens?.accessToken ?? ''
  window.location.href = api.googleBindUrl(currentPageUrl(), t)
}

async function handleUnlinkProvider(provider: string) {
  resetMessages(); loading.value = true
  try {
    await api.unlinkProvider(provider)
    successMsg.value = `${provider} account unlinked successfully!`
    await fetchUserInfo()
  } catch (e: unknown) { error.value = e instanceof Error ? e.message : 'Failed to unlink provider' }
  finally { loading.value = false }
}

function redirectToExternal(data: AuthResponse) {
  const url = new URL(externalRedirect.value!)
  url.searchParams.set('token', data.accessToken!)
  url.searchParams.set('userId', data.userId!.toString())
  window.location.href = url.toString()
}

onMounted(async () => {
  const params = new URLSearchParams(window.location.search)
  const redirect = params.get('redirect')
  if (redirect) externalRedirect.value = redirect

  const authCode = params.get('code')
  const oauthError = params.get('error')

  if (authCode) {
    window.history.replaceState({}, document.title, window.location.pathname)
    try {
      const data = await api.exchangeCode(authCode)
      applyAuthResponse(data)
      successMsg.value = 'OAuth login successful!'
      await fetchUserInfo()
      authStore.transition('profile')
    } catch (e: unknown) { error.value = e instanceof Error ? e.message : 'OAuth login failed' }
  } else if (oauthError) {
    error.value = `OAuth login failed: ${oauthError}`
    window.history.replaceState({}, document.title, window.location.pathname)
  }

  if (appState.value === 'profile') await fetchUserInfo()
})
</script>