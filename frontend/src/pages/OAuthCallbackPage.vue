<template>
  <div class="callback-page">
    <div v-if="error" class="message error">{{ error }}</div>
    <p v-else class="loading-text">Completing sign in...</p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { authStore } from '@/stores/auth'
import { userStore } from '@/stores/user'
import { useExternalRedirect } from '@/stores/externalRedirect'
import * as api from '@/api'
import type { AuthResponse } from '@/api'

const router = useRouter()
const { externalRedirect, clear: clearRedirect } = useExternalRedirect()
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

onMounted(async () => {
  const params = new URLSearchParams(window.location.search)
  const authCode = params.get('code')
  const oauthError = params.get('error')

  if (oauthError) {
    error.value = `OAuth login failed: ${oauthError}`
    return
  }

  if (!authCode) {
    router.push('/login')
    return
  }

  try {
    const data = await api.exchangeCode(authCode)
    applyAuthResponse(data)
    if (externalRedirect.value) {
      redirectToExternal(data)
      return
    }
    await userStore.fetch()
    router.push('/dashboard/profile')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'OAuth login failed'
  }
})
</script>

<style scoped>
.callback-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f0f1f3;
  padding: 20px;
}

.loading-text {
  font-size: 16px;
  color: #555;
}

.message {
  padding: 16px 24px;
  border-radius: 10px;
  font-size: 14px;
}

.message.error {
  background: #fff0f0;
  color: #dc3545;
  border: 1px solid #ffcdd2;
}
</style>
