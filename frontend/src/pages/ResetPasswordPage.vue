<template>
  <div>
    <h2 class="title">Reset password</h2>

    <div v-if="!token" class="message error">
      This reset link is missing its token. Request a new one from the
      <router-link to="/forgot-password">forgot password</router-link> page.
    </div>

    <template v-else-if="!done">
      <p class="hint">Choose a new password. You'll be signed out everywhere else.</p>
      <form @submit.prevent="handleSubmit">
        <div class="form-group">
          <input type="password" v-model="newPassword" placeholder="New password (min. 8 characters)" class="input" minlength="8" required />
        </div>
        <div class="form-group">
          <input type="password" v-model="confirmPassword" placeholder="Confirm new password" class="input" minlength="8" required />
        </div>
        <button type="submit" class="btn btn-primary" :disabled="loading || !newPassword || !confirmPassword">
          {{ loading ? 'Resetting...' : 'Reset password' }}
        </button>
      </form>
      <div v-if="error" class="message error">{{ error }}</div>
    </template>

    <template v-else>
      <div class="message success">
        Your password has been reset and all sessions signed out.
        <router-link to="/login">Sign in with your new password</router-link>.
      </div>
    </template>

    <div class="back-link">
      <router-link to="/login">Back to sign in</router-link>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import * as api from '@/api'

const route = useRoute()
const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : null))

const newPassword = ref('')
const confirmPassword = ref('')
const loading = ref(false)
const error = ref<string | null>(null)
const done = ref(false)

async function handleSubmit() {
  error.value = null
  if (newPassword.value !== confirmPassword.value) {
    error.value = 'Passwords do not match.'
    return
  }
  loading.value = true
  try {
    await api.resetPassword(token.value!, newPassword.value)
    done.value = true
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Reset failed. The link may be expired — request a new one.'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.title { font-size: 20px; font-weight: 700; color: #1a1a2e; margin: 0 0 8px; }
.hint { font-size: 13px; color: #888; margin: 0 0 20px; }
.form-group { margin-bottom: 14px; }
.input { width: 100%; padding: 12px 14px; border: 1.5px solid #e0e0e0; border-radius: 10px; font-size: 14px; transition: border-color .2s; outline: none; background: #fafafa; }
.input:focus { border-color: #333; background: #fff; }
.btn { width: 100%; padding: 12px; border: none; border-radius: 10px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all .2s; }
.btn-primary { background: #1a1a2e; color: #fff; }
.btn-primary:hover:not(:disabled) { background: #2d2d44; box-shadow: 0 4px 12px rgba(0,0,0,.15); transform: translateY(-1px); }
.btn:disabled { opacity: .6; cursor: not-allowed; }
.message { margin-top: 16px; padding: 12px 14px; border-radius: 8px; font-size: 13px; line-height: 1.5; }
.message.error { background: #fff0f0; color: #dc3545; border: 1px solid #ffcdd2; }
.message.success { background: #f0fdf4; color: #16a34a; border: 1px solid #bbf7d0; }
.back-link { margin-top: 20px; text-align: center; font-size: 13px; }
.back-link a { color: #555; text-decoration: none; }
.back-link a:hover { text-decoration: underline; }
</style>
