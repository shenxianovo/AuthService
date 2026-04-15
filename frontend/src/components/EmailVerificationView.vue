<template>
  <div class="email-verify">
    <p class="hint">
      A verification code has been sent to <strong>{{ email }}</strong>.<br />
      Please enter the 6-digit code below.
    </p>

    <form @submit.prevent="handleVerify">
      <div class="form-group">
        <input
          ref="codeInput"
          v-model="code"
          type="text"
          inputmode="numeric"
          pattern="\d{6}"
          maxlength="6"
          placeholder="6-digit code"
          class="input code-input"
          autocomplete="one-time-code"
          required
        />
      </div>

      <button type="submit" class="btn btn-primary" :disabled="loading || code.length !== 6">
        {{ loading ? 'Verifying...' : 'Verify' }}
      </button>
    </form>

    <div class="resend-row">
      <button
        class="btn btn-secondary"
        :disabled="loading || cooldown > 0"
        @click="handleResend"
      >
        {{ cooldown > 0 ? `Resend Code (${cooldown}s)` : 'Resend Code' }}
      </button>
    </div>

    <div v-if="error" class="message error">{{ error }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

defineProps<{
  email: string
  loading: boolean
  error: string
}>()

const emit = defineEmits<{
  verify: [code: string]
  resend: []
}>()

const code = ref('')
const cooldown = ref(0)
const codeInput = ref<HTMLInputElement | null>(null)
let cooldownTimer: ReturnType<typeof setInterval> | null = null

function startCooldown() {
  cooldown.value = 60
  cooldownTimer = setInterval(() => {
    cooldown.value--
    if (cooldown.value <= 0 && cooldownTimer) {
      clearInterval(cooldownTimer)
      cooldownTimer = null
    }
  }, 1000)
}

function handleVerify() {
  if (code.value.length === 6) {
    emit('verify', code.value)
  }
}

function handleResend() {
  emit('resend')
  startCooldown()
}

onMounted(() => {
  // Start initial cooldown since a code was just sent on mount
  startCooldown()
  codeInput.value?.focus()
})

onUnmounted(() => {
  if (cooldownTimer) clearInterval(cooldownTimer)
})
</script>

<style scoped>
.email-verify {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.hint {
  font-size: 0.9rem;
  color: #555;
  line-height: 1.5;
  margin: 0;
}

form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.form-group {
  display: flex;
  flex-direction: column;
}

.code-input {
  letter-spacing: 0.3em;
  text-align: center;
  font-size: 1.4rem;
  font-weight: 600;
}

.resend-row {
  display: flex;
  justify-content: center;
}

.btn-secondary {
  background: transparent;
  border: 1px solid #ccc;
  color: #555;
  padding: 8px 16px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.875rem;
  transition: background 0.15s, color 0.15s;
}

.btn-secondary:hover:not(:disabled) {
  background: #f0f0f0;
  color: #333;
}

.btn-secondary:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
</style>
