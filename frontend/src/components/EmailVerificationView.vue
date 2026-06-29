<template>
  <div class="flex flex-col gap-4">
    <p class="text-sm text-muted-foreground leading-relaxed">
      A verification code has been sent to <strong class="text-foreground">{{ email }}</strong>.<br />
      Please enter the 6-digit code below.
    </p>

    <form class="flex flex-col gap-3" @submit.prevent="handleVerify">
      <Input
        ref="codeInput"
        v-model="code"
        type="text"
        inputmode="numeric"
        pattern="\d{6}"
        maxlength="6"
        placeholder="------"
        autocomplete="one-time-code"
        required
        class="text-center text-2xl font-semibold tracking-[0.3em] h-14"
      />
      <Button type="submit" class="w-full" :disabled="loading || code.length !== 6">
        {{ loading ? 'Verifying...' : 'Verify' }}
      </Button>
    </form>

    <div class="flex justify-center">
      <Button variant="ghost" size="sm" :disabled="loading || cooldown > 0" @click="handleResend">
        {{ cooldown > 0 ? `Resend Code (${cooldown}s)` : 'Resend Code' }}
      </Button>
    </div>

    <div v-if="error" class="px-3.5 py-2.5 rounded-md text-sm text-center bg-destructive/10 text-destructive border border-destructive/20">{{ error }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import type { ComponentPublicInstance } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

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
const codeInput = ref<ComponentPublicInstance | null>(null)
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
  // shadcn Input's root element is the native <input>, so $el is focusable
  ;(codeInput.value?.$el as HTMLInputElement | undefined)?.focus()
})

onUnmounted(() => {
  if (cooldownTimer) clearInterval(cooldownTimer)
})
</script>
