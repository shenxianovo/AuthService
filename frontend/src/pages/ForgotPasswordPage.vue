<template>
  <div>
    <h2 class="title">Forgot password</h2>

    <template v-if="!submitted">
      <p class="hint">Enter your verified email address and we'll send you a reset link.</p>
      <form @submit.prevent="handleSubmit">
        <div class="form-group">
          <input type="email" v-model="email" placeholder="Email address" class="input" required />
        </div>
        <button type="submit" class="btn btn-primary" :disabled="loading || !email">
          {{ loading ? 'Sending...' : 'Send reset link' }}
        </button>
      </form>
    </template>

    <template v-else>
      <div class="message success">
        If an account with a verified email <strong>{{ email }}</strong> exists,
        a reset link is on its way. The link expires in 30 minutes.
      </div>
    </template>

    <div class="back-link">
      <router-link to="/login">Back to sign in</router-link>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import * as api from '@/api'

const email = ref('')
const loading = ref(false)
const submitted = ref(false)

async function handleSubmit() {
  loading.value = true
  try {
    await api.forgotPassword(email.value)
  } catch { /* 204 either way; network errors shouldn't leak more than the success text */ }
  loading.value = false
  submitted.value = true
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
.message.success { background: #f0fdf4; color: #16a34a; border: 1px solid #bbf7d0; }
.back-link { margin-top: 20px; text-align: center; font-size: 13px; }
.back-link a { color: #555; text-decoration: none; }
.back-link a:hover { text-decoration: underline; }
</style>
