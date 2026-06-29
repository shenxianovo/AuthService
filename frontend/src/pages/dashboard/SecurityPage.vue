<template>
  <div>
    <PageHeader title="Security" description="Manage your password and account protection." />

    <Card v-if="userStore.userInfo.value">
      <CardContent class="space-y-4">
        <div class="flex items-center justify-between px-3.5 py-3 rounded-lg bg-muted">
          <span class="text-sm font-medium text-foreground/70">Password</span>
          <span :class="hasPassword ? 'text-primary font-medium text-sm' : 'text-destructive font-medium text-sm'">
            {{ hasPassword ? 'Password set' : 'No password' }}
          </span>
        </div>

        <!-- Change password: proof is the current password, other sessions get signed out -->
        <form v-if="hasPassword" class="flex flex-col gap-2.5 max-w-[420px]" @submit.prevent="handleChangePassword">
          <Input
            v-model="currentPassword"
            type="password"
            placeholder="Current password"
            autocomplete="current-password"
            required
          />
          <Input
            v-model="newPassword"
            type="password"
            placeholder="New password (min. 8 characters)"
            autocomplete="new-password"
            minlength="8"
            required
          />
          <Button type="submit" size="sm" class="self-start" :disabled="loading || !currentPassword || !newPassword">
            Change password
          </Button>
          <p class="text-xs text-muted-foreground">Changing your password signs you out everywhere else.</p>
        </form>

        <!-- Set a first password (OAuth-only account): session is proof enough -->
        <form v-else class="flex flex-col gap-2.5 max-w-[420px]" @submit.prevent="handleAddPassword">
          <Input
            v-model="newPassword"
            type="password"
            placeholder="Set a password (min. 8 characters)"
            autocomplete="new-password"
            minlength="8"
            required
          />
          <Button type="submit" size="sm" class="self-start" :disabled="loading || !newPassword">
            Set password
          </Button>
        </form>
      </CardContent>
    </Card>

    <Toast :message="error" variant="error" />
    <Toast :message="success" variant="success" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import PageHeader from '@/components/PageHeader.vue'
import Toast from '@/components/Toast.vue'
import { userStore } from '@/stores/user'
import * as api from '@/api'

const loading = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)
const currentPassword = ref('')
const newPassword = ref('')

const hasPassword = computed(() => userStore.userInfo.value?.hasPassword ?? false)

async function handleChangePassword() {
  error.value = null
  success.value = null
  loading.value = true
  try {
    await api.changePassword(currentPassword.value, newPassword.value)
    success.value = 'Password changed. Other sessions have been signed out.'
    currentPassword.value = ''
    newPassword.value = ''
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to change password'
  } finally {
    loading.value = false
  }
}

async function handleAddPassword() {
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
