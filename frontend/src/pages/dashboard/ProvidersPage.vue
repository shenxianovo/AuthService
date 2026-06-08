<template>
  <div class="page">
    <h1 class="page-title">Linked Accounts</h1>

    <div class="card" v-if="userStore.userInfo.value">
      <div v-if="providers.length" class="provider-grid">
        <div v-for="p in providers" :key="p.provider" class="provider-card">
          <div class="provider-card-icon" :class="p.provider?.toLowerCase()">
            <svg v-if="p.provider?.toLowerCase() === 'github'" width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>
            <svg v-else-if="p.provider?.toLowerCase() === 'google'" width="20" height="20" viewBox="0 0 24 24"><path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/><path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/><path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/><path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/></svg>
            <span v-else class="provider-letter">{{ p.provider?.charAt(0) }}</span>
          </div>
          <span class="provider-name">{{ p.provider }}</span>
          <button
            v-if="canUnlink"
            class="btn-icon-sm btn-remove"
            @click="handleUnlink(p.provider!)"
            :disabled="loading"
            title="Unlink"
          >&#x2715;</button>
        </div>
      </div>
      <p v-else class="empty-state">No accounts linked yet</p>

      <div v-if="!isGithubLinked || !isGoogleLinked" class="link-actions">
        <button v-if="!isGithubLinked" class="btn btn-github" @click="handleGithubBind" :disabled="loading">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>
          Link GitHub
        </button>
        <button v-if="!isGoogleLinked" class="btn btn-google" @click="handleGoogleBind" :disabled="loading">
          <svg width="16" height="16" viewBox="0 0 24 24"><path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/><path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/><path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/><path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/></svg>
          Link Google
        </button>
      </div>
    </div>

    <div v-if="error" class="toast error">{{ error }}</div>
    <div v-if="success" class="toast success">{{ success }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { authStore } from '@/stores/auth'
import { userStore } from '@/stores/user'
import * as api from '@/api'

const loading = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)

const providers = computed(() => userStore.userInfo.value?.providers ?? [])

const linkedProviders = computed(() =>
  new Set(providers.value.map(p => p.provider?.toLowerCase()))
)
const isGithubLinked = computed(() => linkedProviders.value.has('github'))
const isGoogleLinked = computed(() => linkedProviders.value.has('google'))

const canUnlink = computed(() => {
  const providerCount = providers.value.length
  const hasPassword = userStore.userInfo.value?.hasPassword ?? false
  return hasPassword || providerCount > 1
})

function resetMessages() { error.value = null; success.value = null }

async function handleUnlink(provider: string) {
  resetMessages()
  loading.value = true
  try {
    await api.unlinkProvider(provider)
    success.value = `${provider} account unlinked`
    await userStore.fetch()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to unlink provider'
  } finally {
    loading.value = false
  }
}

function handleGithubBind() {
  const t = authStore.state.tokens?.accessToken ?? ''
  window.location.href = api.githubBindUrl(window.location.origin + '/callback', t)
}

function handleGoogleBind() {
  const t = authStore.state.tokens?.accessToken ?? ''
  window.location.href = api.googleBindUrl(window.location.origin + '/callback', t)
}

onMounted(() => {
  if (!userStore.userInfo.value) userStore.fetch()
})
</script>

<style scoped>
.page-title {
  font-size: 22px;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 24px;
}

.card {
  background: #fff;
  border-radius: 14px;
  padding: 24px;
  box-shadow: 0 2px 12px rgba(0,0,0,.05);
}

.provider-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 16px;
}

.provider-card {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 16px;
  background: #f8f9fb;
  border-radius: 10px;
  min-width: 140px;
}

.provider-card-icon {
  width: 36px;
  height: 36px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
}
.provider-card-icon.github { background: #24292e; }
.provider-card-icon.google { background: #fff; border: 1px solid #e0e0e0; }

.provider-name {
  font-size: 14px;
  font-weight: 500;
  color: #333;
  text-transform: capitalize;
}

.provider-letter {
  font-weight: 700;
  font-size: 16px;
}

.link-actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  padding-top: 16px;
  border-top: 1px solid #f0f0f0;
}

.toast {
  position: fixed;
  bottom: 24px;
  left: 50%;
  transform: translateX(-50%);
  padding: 12px 24px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  z-index: 1000;
  box-shadow: 0 8px 32px rgba(0,0,0,.12);
}
.toast.error { background: #fef2f2; color: #dc2626; border: 1px solid #fecaca; }
.toast.success { background: #f0fdf4; color: #16a34a; border: 1px solid #bbf7d0; }
</style>
