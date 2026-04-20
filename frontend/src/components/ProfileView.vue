<template>
  <div class="dashboard">
    <!-- Top Navigation Bar -->
    <header class="topbar">
      <div class="topbar-inner">
        <div class="topbar-brand">
          <span class="brand-icon"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2a5 5 0 0 1 5 5v3a5 5 0 0 1-10 0V7a5 5 0 0 1 5-5z"/><path d="M3.5 14.5C3.5 9 7 8 12 8s8.5 1 8.5 6.5c0 4-3.5 7.5-8.5 7.5s-8.5-3.5-8.5-7.5z"/><path d="M8 14v.5"/><path d="M16 14v.5"/></svg></span>
          <span class="brand-text">AuthService</span>
        </div>
        <div class="topbar-user">
          <div class="topbar-avatar">{{ userInitial }}</div>
          <span class="topbar-name">{{ userInfo?.displayName ?? 'User' }}</span>
          <button class="btn-logout" @click="$emit('logout')" title="Sign out">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
          </button>
        </div>
      </div>
    </header>

    <!-- Main Content -->
    <main class="dashboard-main">
      <div class="dashboard-grid">
        <!-- Profile Card -->
        <section class="card card-profile">
          <div class="card-header">
            <h3>Profile</h3>
          </div>
          <div class="card-body">
            <div class="profile-hero">
              <div class="avatar-large">{{ userInitial }}</div>
              <div class="profile-meta">
                <h2 class="profile-name">{{ userInfo?.displayName ?? 'User' }}</h2>
                <p class="profile-id">ID: {{ userId }}</p>
              </div>
            </div>
          </div>
        </section>

        <!-- Email Management Card -->
        <section class="card card-emails">
          <div class="card-header">
            <h3><svg class="section-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="4" width="20" height="16" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg> Email Addresses</h3>
          </div>
          <div class="card-body" v-if="userInfo">
            <div class="email-list">
              <div v-for="email in userInfo.emails" :key="email.email" class="email-item">
                <div class="email-address">
                  <span class="email-text">{{ email.email }}</span>
                </div>
                <div class="email-badges">
                  <span v-if="email.isPrimary" class="badge badge-primary">Primary</span>
                  <span v-if="email.isVerified" class="badge badge-success">Verified</span>
                  <button
                    v-else
                    class="badge badge-warning badge-btn"
                    @click="$emit('verifyEmail')"
                    :disabled="loading"
                  >Verify</button>
                  <button
                    v-if="!email.isPrimary && email.isVerified"
                    class="badge badge-info badge-btn"
                    @click="emit('setPrimaryEmail', email.email!)"
                    :disabled="loading"
                  >Set Primary</button>
                  <button
                    v-if="!email.isPrimary"
                    class="btn-icon-sm btn-remove"
                    @click="emit('removeEmail', email.email!)"
                    :disabled="loading"
                    title="Remove email"
                  >✕</button>
                </div>
              </div>
            </div>
            <div class="add-email-row">
              <input
                type="email"
                v-model="newEmailInput"
                placeholder="Add new email address..."
                class="input"
                @keyup.enter="submitAddEmail"
                :disabled="loading"
              />
              <button
                class="btn btn-sm"
                @click="submitAddEmail"
                :disabled="loading || !newEmailInput.trim()"
              >Add</button>
            </div>
            <p v-if="emailFormatError" class="error-text">{{ emailFormatError }}</p>
          </div>
        </section>

        <!-- Linked Accounts Card -->
        <section class="card card-providers">
          <div class="card-header">
            <h3><svg class="section-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg> Linked Accounts</h3>
          </div>
          <div class="card-body" v-if="userInfo">
            <div v-if="userInfo.providers && userInfo.providers.length" class="provider-grid">
              <div v-for="p in userInfo.providers" :key="p.provider" class="provider-card">
                <div class="provider-card-icon" :class="p.provider?.toLowerCase()">
                  <svg v-if="p.provider?.toLowerCase() === 'github'" width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>
                  <svg v-else-if="p.provider?.toLowerCase() === 'google'" width="20" height="20" viewBox="0 0 24 24"><path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/><path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/><path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/><path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/></svg>
                  <span v-else class="provider-letter">{{ p.provider?.charAt(0) }}</span>
                </div>
                <span class="provider-name">{{ p.provider }}</span>
                <button
                  v-if="canUnlink"
                  class="btn-icon-sm btn-remove"
                  @click="$emit('unlinkProvider', p.provider!)"
                  :disabled="loading"
                  title="Unlink"
                >✕</button>
              </div>
            </div>
            <p v-else class="empty-state">No accounts linked yet</p>

            <div v-if="!isGithubLinked || !isGoogleLinked" class="link-actions">
              <button v-if="!isGithubLinked" class="btn btn-github" @click="$emit('githubBind')" :disabled="loading">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>
                Link GitHub
              </button>
              <button v-if="!isGoogleLinked" class="btn btn-google" @click="$emit('googleBind')" :disabled="loading">
                <svg width="16" height="16" viewBox="0 0 24 24"><path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/><path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/><path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/><path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/></svg>
                Link Google
              </button>
            </div>
          </div>
        </section>

        <!-- Security Settings Card -->
        <section class="card card-security">
          <div class="card-header">
            <h3><svg class="section-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg> Security</h3>
          </div>
          <div class="card-body" v-if="userInfo">
            <div class="security-item">
              <div class="security-label">Password</div>
              <div class="security-status">
                <span :class="userInfo.hasPassword ? 'status-set' : 'status-unset'">
                  {{ userInfo.hasPassword ? '✓ Password set' : '✗ No password' }}
                </span>
              </div>
            </div>
            <div class="password-form">
              <input
                type="password"
                :value="newPassword"
                @input="$emit('update:newPassword', ($event.target as HTMLInputElement).value)"
                :placeholder="userInfo.hasPassword ? 'Change password' : 'Set a password'"
                class="input"
              />
              <button class="btn btn-sm" @click="$emit('addPassword')" :disabled="loading || !newPassword">
                {{ userInfo.hasPassword ? 'Update' : 'Set' }}
              </button>
            </div>
          </div>
        </section>
      </div>
    </main>
  </div>
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

const canUnlink = computed(() => {
  const providerCount = props.userInfo?.providers?.length ?? 0
  const hasPassword = props.userInfo?.hasPassword ?? false
  return hasPassword || providerCount > 1
})
</script>

<style scoped>
.dashboard {
  position: fixed;
  inset: 0;
  display: flex;
  flex-direction: column;
  background: #f4f6f9;
  overflow: auto;
}

.topbar {
  background: #fff;
  border-bottom: 1px solid #e8ecf0;
  padding: 0 24px;
  height: 60px;
  display: flex;
  align-items: center;
  flex-shrink: 0;
  position: sticky;
  top: 0;
  z-index: 100;
  box-shadow: 0 1px 3px rgba(0,0,0,.04);
}

.topbar-inner {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.topbar-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 18px;
  font-weight: 700;
  color: #1a1a2e;
}

.brand-icon {
  display: flex;
  align-items: center;
  color: #1a1a2e;
}

.topbar-user {
  display: flex;
  align-items: center;
  gap: 12px;
}

.topbar-avatar {
  width: 34px;
  height: 34px;
  border-radius: 50%;
  background: #1a1a2e;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 700;
}

.topbar-name {
  font-size: 14px;
  font-weight: 500;
  color: #444;
}

.btn-logout {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border: none;
  background: transparent;
  border-radius: 8px;
  color: #888;
  cursor: pointer;
  transition: all .2s;
}

.btn-logout:hover {
  background: #fee2e2;
  color: #dc2626;
}

.dashboard-main {
  flex: 1;
  padding: 32px 24px;
  max-width: 1200px;
  margin: 0 auto;
  width: 100%;
}

.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 24px;
}

.card {
  background: #fff;
  border-radius: 14px;
  box-shadow: 0 2px 12px rgba(0,0,0,.05);
  overflow: hidden;
  transition: box-shadow .2s;
}

.card:hover {
  box-shadow: 0 4px 20px rgba(0,0,0,.08);
}

.card-header {
  padding: 20px 24px 0;
}

.card-header h3 {
  font-size: 16px;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.section-icon {
  flex-shrink: 0;
  color: #555;
}

.card-body {
  padding: 16px 24px 24px;
}

.card-profile {
  grid-column: 1 / -1;
}

.profile-hero {
  display: flex;
  align-items: center;
  gap: 20px;
}

.avatar-large {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: #1a1a2e;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  font-weight: 700;
  flex-shrink: 0;
}

.profile-name {
  font-size: 22px;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 4px;
}

.profile-id {
  font-size: 12px;
  color: #999;
  font-family: 'SF Mono', Monaco, 'Cascadia Code', monospace;
  margin: 0;
  word-break: break-all;
}

.email-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 16px;
}

.email-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 14px;
  background: #f8f9fb;
  border-radius: 10px;
  gap: 12px;
  flex-wrap: wrap;
}

.email-text {
  font-size: 14px;
  color: #333;
  word-break: break-all;
}

.email-badges {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.add-email-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.add-email-row .input { flex: 1; min-width: 0; }

.error-text {
  color: #dc3545;
  font-size: 12px;
  margin-top: 6px;
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
  padding-top: 12px;
  border-top: 1px solid #f0f0f0;
}

.empty-state {
  color: #aaa;
  font-size: 14px;
  margin: 8px 0;
}

.security-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  padding: 12px 14px;
  background: #f8f9fb;
  border-radius: 10px;
}

.security-label {
  font-size: 14px;
  font-weight: 500;
  color: #555;
}

.status-set {
  color: #16a34a;
  font-weight: 500;
  font-size: 13px;
}

.status-unset {
  color: #dc2626;
  font-weight: 500;
  font-size: 13px;
}

.password-form {
  display: flex;
  gap: 8px;
  align-items: center;
}

.password-form .input { flex: 1; min-width: 0; }

.input {
  padding: 8px 12px;
  border: 1.5px solid #e0e0e0;
  border-radius: 8px;
  font-size: 13px;
  transition: border-color .2s, box-shadow .2s;
  outline: none;
  background: #fff;
}

.input:focus {
  border-color: #333;
  box-shadow: 0 0 0 3px rgba(0, 0, 0, .06);
}

.btn {
  padding: 10px 18px;
  border: none;
  border-radius: 10px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all .2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  white-space: nowrap;
  background: #1a1a2e;
  color: #fff;
}

.btn:hover:not(:disabled) {
  background: #2d2d44;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(0,0,0,.12);
}

.btn:disabled {
  opacity: .5;
  cursor: not-allowed;
  transform: none;
}

.btn-sm {
  padding: 6px 14px;
  font-size: 12px;
  width: auto;
  flex-shrink: 0;
}

.btn-github { background: #24292e; color: #fff; }
.btn-github:hover:not(:disabled) { background: #1b1f23; }

.btn-google { background: #fff; color: #333; border: 1.5px solid #e0e0e0; }
.btn-google:hover:not(:disabled) { background: #f8f8f8; border-color: #ccc; }

.badge {
  padding: 3px 10px;
  border-radius: 20px;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: .3px;
  white-space: nowrap;
}

.badge-primary { background: #eef0f2; color: #555; }
.badge-success { background: #dcfce7; color: #16a34a; }
.badge-warning { background: #fef3c7; color: #d97706; border: 1px solid #fbbf24; }
.badge-info { background: #e0e7ff; color: #4f46e5; border: 1px solid #a5b4fc; }

.badge-btn {
  cursor: pointer;
  transition: all .15s;
}

.badge-btn:hover:not(:disabled) {
  filter: brightness(0.9);
  transform: scale(1.05);
}

.badge-btn:disabled {
  opacity: .5;
  cursor: not-allowed;
}

.btn-icon-sm {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  padding: 0;
  border: none;
  background: transparent;
  border-radius: 50%;
  color: #aaa;
  font-size: 12px;
  cursor: pointer;
  transition: all .15s;
}

.btn-remove:hover:not(:disabled) {
  background: #fee2e2;
  color: #dc2626;
}

.btn-icon-sm:disabled {
  opacity: .4;
  cursor: not-allowed;
}

@media (max-width: 900px) {
  .dashboard-grid {
    grid-template-columns: 1fr;
  }
  .card-profile {
    grid-column: 1;
  }
  .dashboard-main {
    padding: 20px 16px;
  }
}

@media (max-width: 600px) {
  .topbar {
    padding: 0 16px;
    height: 54px;
  }
  .topbar-name {
    display: none;
  }
  .brand-text {
    font-size: 16px;
  }
  .dashboard-main {
    padding: 16px 12px;
  }
  .dashboard-grid {
    gap: 16px;
  }
  .card-body {
    padding: 12px 16px 20px;
  }
  .card-header {
    padding: 16px 16px 0;
  }
  .profile-hero {
    flex-direction: column;
    text-align: center;
  }
  .email-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 8px;
  }
  .add-email-row {
    flex-direction: column;
  }
  .password-form {
    flex-direction: column;
  }
  .link-actions {
    flex-direction: column;
  }
  .link-actions .btn {
    width: 100%;
  }
}
</style>