<template>
  <div>
    <PageHeader title="Email Addresses" description="Manage the email addresses linked to your account." />

    <Card v-if="userStore.userInfo.value">
      <CardContent class="space-y-4">
        <div class="flex flex-col gap-2">
          <div
            v-for="email in userStore.userInfo.value.emails"
            :key="email.email"
            class="flex items-center justify-between gap-3 flex-wrap px-3.5 py-2.5 rounded-lg bg-muted"
          >
            <span class="text-sm text-foreground break-all">{{ email.email }}</span>
            <div class="flex items-center gap-1.5 flex-wrap">
              <Badge v-if="email.isPrimary" variant="secondary">Primary</Badge>
              <Badge v-if="email.isVerified" variant="default">Verified</Badge>
              <button
                v-else
                :class="cn(badgeVariants({ variant: 'outline' }), 'cursor-pointer border-amber-400 text-amber-600 hover:bg-amber-50 disabled:opacity-50')"
                :disabled="loading"
                @click="handleVerifyEmail(email.email!)"
              >Verify</button>
              <button
                v-if="!email.isPrimary && email.isVerified"
                :class="cn(badgeVariants({ variant: 'outline' }), 'cursor-pointer hover:bg-accent disabled:opacity-50')"
                :disabled="loading"
                @click="handleSetPrimary(email.email!)"
              >Set Primary</button>
              <button
                v-if="!email.isPrimary"
                class="inline-flex items-center justify-center size-[22px] rounded-full text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors disabled:opacity-40"
                :disabled="loading"
                title="Remove email"
                @click="handleRemove(email.email!)"
              >&#x2715;</button>
            </div>
          </div>
        </div>

        <div class="flex gap-2 items-center">
          <Input
            v-model="newEmail"
            type="email"
            placeholder="Add new email address..."
            class="flex-1"
            :disabled="loading"
            @keyup.enter="handleAdd"
          />
          <Button size="sm" :disabled="loading || !newEmail.trim()" @click="handleAdd">Add</Button>
        </div>
        <p v-if="emailError" class="text-xs text-destructive">{{ emailError }}</p>
      </CardContent>
    </Card>

    <Toast :message="error" variant="error" />
    <Toast :message="success" variant="success" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { cn } from '@/lib/utils'
import { Card, CardContent } from '@/components/ui/card'
import { Badge, badgeVariants } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import PageHeader from '@/components/PageHeader.vue'
import Toast from '@/components/Toast.vue'
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
