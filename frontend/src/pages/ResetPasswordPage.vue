<template>
  <div>
    <h2 class="text-xl font-bold text-foreground mb-2">Reset password</h2>

    <div v-if="!token" class="mt-4 px-3.5 py-3 rounded-md text-sm leading-relaxed bg-destructive/10 text-destructive border border-destructive/20">
      This reset link is missing its token. Request a new one from the
      <router-link to="/forgot-password" class="underline">forgot password</router-link> page.
    </div>

    <template v-else-if="!done">
      <p class="text-sm text-muted-foreground mb-5">Choose a new password. You'll be signed out everywhere else.</p>
      <form class="space-y-4" @submit.prevent="handleSubmit">
        <div class="space-y-2">
          <Label for="new-password">New password</Label>
          <Input id="new-password" v-model="newPassword" type="password" placeholder="Min. 8 characters" minlength="8" autocomplete="new-password" required />
        </div>
        <div class="space-y-2">
          <Label for="confirm-password">Confirm password</Label>
          <Input id="confirm-password" v-model="confirmPassword" type="password" placeholder="Re-enter new password" minlength="8" autocomplete="new-password" required />
        </div>
        <Button type="submit" class="w-full" :disabled="loading || !newPassword || !confirmPassword">
          {{ loading ? 'Resetting...' : 'Reset password' }}
        </Button>
      </form>
      <div v-if="error" class="mt-4 px-3.5 py-3 rounded-md text-sm bg-destructive/10 text-destructive border border-destructive/20">{{ error }}</div>
    </template>

    <template v-else>
      <div class="px-3.5 py-3 rounded-md text-sm leading-relaxed bg-primary-soft text-primary-dark border border-primary/20">
        Your password has been reset and all sessions signed out.
        <router-link to="/login" class="underline">Sign in with your new password</router-link>.
      </div>
    </template>

    <div class="mt-5 text-center text-sm">
      <router-link to="/login" class="text-muted-foreground hover:text-foreground hover:underline">Back to sign in</router-link>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
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
