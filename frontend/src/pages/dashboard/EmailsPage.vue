<template>
  <div class="page">
    <h1 class="page-title">Email Addresses</h1>

    <div class="card" v-if="userStore.userInfo.value">
      <div class="email-list">
        <div v-for="email in userStore.userInfo.value.emails" :key="email.email" class="list-item">
          <div class="email-address">
            <span class="email-text">{{ email.email }}</span>
          </div>
          <div class="email-badges">
            <span v-if="email.isPrimary" class="badge badge-primary">Primary</span>
            <span v-if="email.isVerified" class="badge badge-success">Verified</span>
            <button
              v-else
              class="badge badge-warning badge-btn"
              @click="handleVerifyEmail(email.email!)"
              :disabled="loading"
            >Verify</button>
            <button
              v-if="!email.isPrimary && email.isVerified"
              class="badge badge-info badge-btn"
              @click="handleSetPrimary(email.email!)"
              :disabled="loading"
            >Set Primary</button>
            <button
              v-if="!email.isPrimary"
              class="btn-icon-sm btn-remove"
              @click="handleRemove(email.email!)"
              :disabled="loading"
              title="Remove email"
            >&#x2715;</button>
          </div>
        </div>
      </div>

      <div class="form-row">
        <input
          type="email"
          v-model="newEmail"
          placeholder="Add new email address..."
          class="input"
          @keyup.enter="handleAdd"
          :disabled="loading"
        />
        <button
          class="btn btn-sm"
          @click="handleAdd"
          :disabled="loading || !newEmail.trim()"
        >Add</button>
      </div>
      <p v-if="emailError" class="error-text">{{ emailError }}</p>
    </div>

    <div v-if="error" class="toast error">{{ error }}</div>
    <div v-if="success" class="toast success">{{ success }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { userStore } from '@/stores/user'
import * as api from '@/api'

const router = useRouter()
const loading = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)
const newEmail = ref('')
const emailError = ref('')

function resetMessages() { error.value = null; success.value = null }

async function handleAdd() {
  const email = newEmail.value.trim()
  if (!email) return
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    emailError.value = 'Please enter a valid email address'
    return
  }
  emailError.value = ''
  resetMessages()
  loading.value = true
  try {
    await api.addEmail(email)
    newEmail.value = ''
    await userStore.fetch()
    // Navigate to verify-email with the new email
    router.push({ name: 'verify-email', query: { email, emailId: email } })
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to add email'
  } finally {
    loading.value = false
  }
}

async function handleRemove(email: string) {
  resetMessages()
  loading.value = true
  try {
    await api.removeEmail(email)
    await userStore.fetch()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to remove email'
  } finally {
    loading.value = false
  }
}

async function handleSetPrimary(email: string) {
  resetMessages()
  loading.value = true
  try {
    await api.setPrimaryEmail(email)
    await userStore.fetch()
    success.value = `${email} is now your primary email`
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to set primary email'
  } finally {
    loading.value = false
  }
}

async function handleVerifyEmail(email: string) {
  resetMessages()
  loading.value = true
  try {
    await api.sendVerificationCode(email)
    router.push({ name: 'verify-email', query: { email, emailId: email } })
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to send verification code'
  } finally {
    loading.value = false
  }
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

.email-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 16px;
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
