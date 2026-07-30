<template>
  <div>
    <PageHeader :title="t('security.title')" :description="t('security.description')" />

    <Card v-if="userStore.userInfo.value">
      <CardContent class="space-y-4">
        <div class="flex items-center justify-between px-3.5 py-3 rounded-lg bg-muted">
          <span class="text-sm font-medium text-foreground/70">{{ t('security.password') }}</span>
          <span :class="hasPassword ? 'text-primary font-medium text-sm' : 'text-destructive font-medium text-sm'">
            {{ hasPassword ? t('security.passwordSet') : t('security.noPassword') }}
          </span>
        </div>

        <!-- Change password: proof is the current password, other sessions get signed out -->
        <form v-if="hasPassword" class="flex flex-col gap-2.5 max-w-[420px]" @submit.prevent="handleChangePassword">
          <Input
            v-model="currentPassword"
            type="password"
            :placeholder="t('security.currentPlaceholder')"
            autocomplete="current-password"
            required
          />
          <Input
            v-model="newPassword"
            type="password"
            :placeholder="t('security.newPlaceholder')"
            autocomplete="new-password"
            minlength="8"
            required
          />
          <Button type="submit" size="sm" class="self-start" :disabled="loading || !currentPassword || !newPassword">
            {{ t('security.change') }}
          </Button>
          <p class="text-xs text-muted-foreground">{{ t('security.changeNote') }}</p>
        </form>

        <!-- Set a first password (OAuth-only account): session is proof enough -->
        <form v-else class="flex flex-col gap-2.5 max-w-[420px]" @submit.prevent="handleAddPassword">
          <Input
            v-model="newPassword"
            type="password"
            :placeholder="t('security.setPlaceholder')"
            autocomplete="new-password"
            minlength="8"
            required
          />
          <Button type="submit" size="sm" class="self-start" :disabled="loading || !newPassword">
            {{ t('security.set') }}
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
import { useI18n } from 'vue-i18n'
import { translateApiError } from '@/i18n'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import PageHeader from '@/components/PageHeader.vue'
import Toast from '@/components/Toast.vue'
import { userStore } from '@/stores/user'
import * as api from '@/api'

const { t } = useI18n()

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
    success.value = t('security.changed')
    currentPassword.value = ''
    newPassword.value = ''
  } catch (e: unknown) {
    error.value = translateApiError(e, t('security.changeFailed'))
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
    success.value = t('security.setSuccess')
    newPassword.value = ''
    await userStore.fetch()
  } catch (e: unknown) {
    error.value = translateApiError(e, t('security.setFailed'))
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  if (!userStore.userInfo.value) userStore.fetch()
})
</script>
