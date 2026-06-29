<template>
  <div>
    <h2 class="text-xl font-bold text-foreground mb-2">Forgot password</h2>

    <template v-if="!submitted">
      <p class="text-sm text-muted-foreground mb-5">Enter your verified email address and we'll send you a reset link.</p>
      <form class="space-y-4" @submit.prevent="handleSubmit">
        <div class="space-y-2">
          <Label for="forgot-email">Email</Label>
          <Input id="forgot-email" v-model="email" type="email" placeholder="you@example.com" required />
        </div>
        <Button type="submit" class="w-full" :disabled="loading || !email">
          {{ loading ? 'Sending...' : 'Send reset link' }}
        </Button>
      </form>
    </template>

    <template v-else>
      <div class="px-3.5 py-3 rounded-md text-sm leading-relaxed bg-primary-soft text-primary-dark border border-primary/20">
        If an account with a verified email <strong>{{ email }}</strong> exists,
        a reset link is on its way. The link expires in 30 minutes.
      </div>
    </template>

    <div class="mt-5 text-center text-sm">
      <router-link to="/login" class="text-muted-foreground hover:text-foreground hover:underline">Back to sign in</router-link>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
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
