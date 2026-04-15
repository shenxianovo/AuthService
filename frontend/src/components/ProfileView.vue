<template>
  <div class="profile-section">
    <div class="avatar">{{ userInitial }}</div>
    <div class="profile-info">
      <h2>{{ userInfo ? userInfo.displayName : 'User' }}</h2>
      <p class="user-id">{{ userId }}</p>
    </div>
  </div>

  <div v-if="userInfo" class="details-section">
    <div class="detail-group">
      <div class="detail-label">Emails</div>
      <div v-for="email in userInfo.emails" :key="email.email" class="detail-item">
        <span>{{ email.email }}</span>
        <div class="badge-group">
          <span v-if="email.isPrimary" class="badge badge-primary">Primary</span>
          <span v-if="email.isVerified" class="badge badge-success">Verified</span>
          <button
            v-else
            class="btn-verify-email"
            @click="$emit('verifyEmail')"
            :disabled="loading"
          >Verify</button>
          <button
            v-if="!email.isPrimary && email.isVerified"
            class="btn-set-primary"
            @click="emit('setPrimaryEmail', email.email!)"
            :disabled="loading"
          >Set Primary</button>
          <button
            v-if="!email.isPrimary"
            class="btn-unlink"
            @click="emit('removeEmail', email.email!)"
            :disabled="loading"
            title="Remove email"
          >✕</button>
        </div>
      </div>
      <div class="input-row" style="margin-top: 8px;">
        <input
          type="email"
          v-model="newEmailInput"
          placeholder="Add email address"
          class="input"
          @keyup.enter="submitAddEmail"
          :disabled="loading"
        />
        <button
          class="btn btn-secondary"
          @click="submitAddEmail"
          :disabled="loading || !newEmailInput.trim()"
        >Add</button>
      </div>
      <p v-if="emailFormatError" class="error-text">{{ emailFormatError }}</p>
    </div>
    <div class="detail-group">
      <div class="detail-label">Linked Accounts</div>
      <div v-if="userInfo.providers && userInfo.providers.length" class="provider-list">
        <div v-for="p in userInfo.providers" :key="p.provider" class="provider-chip">
          <span class="provider-icon" :class="p.provider?.toLowerCase()"></span>
          {{ p.provider }}
          <button
            v-if="canUnlink"
            class="btn-unlink"
            @click="$emit('unlinkProvider', p.provider!)"
            :disabled="loading"
            title="Unlink"
          >✕</button>
        </div>
      </div>
      <p v-else class="muted">No accounts linked yet</p>
    </div>
    <div class="detail-group">
      <div class="detail-label">Password</div>
      <div class="detail-item">
        <span :class="userInfo.hasPassword ? 'status-set' : 'status-unset'">
          {{ userInfo.hasPassword ? 'Set' : 'Not set' }}
        </span>
      </div>
    </div>
  </div>

  <div class="settings-section">
    <h3>Account Settings</h3>
    <div class="input-row">
      <input type="password" :value="newPassword" @input="$emit('update:newPassword', ($event.target as HTMLInputElement).value)" placeholder="New password" class="input" />
      <button class="btn btn-secondary" @click="$emit('addPassword')" :disabled="loading || !newPassword">Set</button>
    </div>
    <div v-if="!isGithubLinked || !isGoogleLinked" class="oauth-divider"><span>Link accounts</span></div>
    <div v-if="!isGithubLinked || !isGoogleLinked" class="oauth-buttons">
      <button v-if="!isGithubLinked" class="btn btn-github" @click="$emit('githubBind')" :disabled="loading">
        <svg class="icon" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>
        GitHub
      </button>
      <button v-if="!isGoogleLinked" class="btn btn-google" @click="$emit('googleBind')" :disabled="loading">
        <svg class="icon" viewBox="0 0 24 24"><path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/><path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/><path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/><path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/></svg>
        Google
      </button>
    </div>
  </div>

  <button @click="$emit('logout')" class="btn btn-danger">Sign out</button>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { UserInfoResponse } from '@/api'

const props = defineProps<{
  userInfo: UserInfoResponse | null
  userId: string
  newPassword: string
  loading: boolean
}>()

const emit = defineEmits<{
  addPassword: []
  githubBind: []
  googleBind: []
  unlinkProvider: [provider: string]
  verifyEmail: []
  addEmail: [email: string]
  removeEmail: [email: string]
  setPrimaryEmail: [email: string]
  logout: []
  'update:newPassword': [value: string]
}>()

const newEmailInput = ref('')

const emailFormatError = ref('')

function submitAddEmail() {
  const email = newEmailInput.value.trim()
  if (!email) return
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    emailFormatError.value = '请输入有效的邮箱地址'
    return
  }
  emailFormatError.value = ''
  emit('addEmail', email)
  newEmailInput.value = ''
}

const userInitial = computed(() => {
  const n = props.userInfo?.displayName
  return n ? n.charAt(0).toUpperCase() : '?'
})

const linkedProviders = computed(() =>
  new Set((props.userInfo?.providers ?? []).map(p => p.provider?.toLowerCase()))
)

const isGithubLinked = computed(() => linkedProviders.value.has('github'))
const isGoogleLinked = computed(() => linkedProviders.value.has('google'))

/** Can unlink only when the user has more than one login method available */
const canUnlink = computed(() => {
  const providerCount = props.userInfo?.providers?.length ?? 0
  const hasPassword = props.userInfo?.hasPassword ?? false
  return hasPassword || providerCount > 1
})
</script>