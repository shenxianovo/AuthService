<template>
  <div class="page">
    <h1 class="page-title">Security</h1>

    <div class="card" v-if="userStore.userInfo.value">
      <div class="security-item">
        <div class="security-label">Password</div>
        <div class="security-status">
          <span :class="userStore.userInfo.value.hasPassword ? 'status-set' : 'status-unset'">
            {{ userStore.userInfo.value.hasPassword ? 'Password set' : 'No password' }}
          </span>
        </div>
      </div>

      <div class="form-row">
        <input
          type="password"
          v-model="newPassword"
          :placeholder="userStore.userInfo.value.hasPassword ? 'Change password' : 'Set a password'"
          class="input"
          @keyup.enter="handleAddPassword"
        />
        <button class="btn btn-sm" @click="handleAddPassword" :disabled="loading || !newPassword">
          {{ userStore.userInfo.value.hasPassword ? 'Update' : 'Set' }}
        </button>
      </div>
    </div>

    <div v-if="error" class="toast error">{{ error }}</div>
    <div v-if="success" class="toast success">{{ success }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { userStore } from '@/stores/user'
import * as api from '@/api'

const loading = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)
const newPassword = ref('')

async function handleAddPassword() {
  if (!newPassword.value) return
  error.value = null
  success.value = null
  loading.value = true
  try {
    await api.addPassword(newPassword.value)
    success.value = 'Password successfully set!'
    newPassword.value = ''
    await userStore.fetch()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to set password'
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
