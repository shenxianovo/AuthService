<template>
  <div class="min-h-screen flex items-center justify-center p-5">
    <div v-if="error" class="px-6 py-4 rounded-lg text-sm bg-destructive/10 text-destructive border border-destructive/20">{{ error }}</div>
    <p v-else class="text-base text-muted-foreground">Completing sign in...</p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { authStore } from '@/stores/auth'
import { userStore } from '@/stores/user'
import { useExternalRedirect } from '@/stores/externalRedirect'
import { useOidcReturnUrl } from '@/stores/oidcReturnUrl'
import * as api from '@/api'
import type { AuthResponse } from '@/api'

const router = useRouter()
const { externalRedirect, clear: clearRedirect } = useExternalRedirect()
const { returnUrl: oidcReturnUrl, consume: consumeOidcReturnUrl } = useOidcReturnUrl()
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
    // OIDC flow: the exchange response just set the SSO cookie; resume the
    // authorize request with a full-page navigation so the cookie is sent.
    if (oidcReturnUrl.value) {
      window.location.href = consumeOidcReturnUrl()!
      return
    }
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
