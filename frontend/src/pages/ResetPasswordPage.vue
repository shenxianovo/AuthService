<template>
  <div>
    <h2 class="text-xl font-bold text-foreground mb-2">{{ t('resetPassword.title') }}</h2>

    <div v-if="!token" class="mt-4 px-3.5 py-3 rounded-md text-sm leading-relaxed bg-destructive/10 text-destructive border border-destructive/20">
      <i18n-t keypath="resetPassword.missingToken" tag="span">
        <template #link><router-link to="/forgot-password" class="underline">{{ t('resetPassword.missingTokenLink') }}</router-link></template>
      </i18n-t>
    </div>

    <template v-else-if="!done">
      <p class="text-sm text-muted-foreground mb-5">{{ t('resetPassword.intro') }}</p>
      <form class="space-y-4" @submit.prevent="handleSubmit">
        <div class="space-y-2">
          <Label for="new-password">{{ t('resetPassword.newPassword') }}</Label>
          <Input id="new-password" v-model="newPassword" type="password" :placeholder="t('resetPassword.newPasswordPlaceholder')" minlength="8" autocomplete="new-password" required />
        </div>
        <div class="space-y-2">
          <Label for="confirm-password">{{ t('resetPassword.confirmPassword') }}</Label>
          <Input id="confirm-password" v-model="confirmPassword" type="password" :placeholder="t('resetPassword.confirmPasswordPlaceholder')" minlength="8" autocomplete="new-password" required />
        </div>
        <Button type="submit" class="w-full" :disabled="loading || !newPassword || !confirmPassword">
          {{ loading ? t('resetPassword.resetting') : t('resetPassword.reset') }}
        </Button>
      </form>
      <div v-if="error" class="mt-4 px-3.5 py-3 rounded-md text-sm bg-destructive/10 text-destructive border border-destructive/20">{{ error }}</div>
    </template>

    <template v-else>
      <div class="px-3.5 py-3 rounded-md text-sm leading-relaxed bg-primary-soft text-primary-dark border border-primary/20">
        <i18n-t keypath="resetPassword.done" tag="span">
          <template #link><router-link to="/login" class="underline">{{ t('resetPassword.doneLink') }}</router-link></template>
        </i18n-t>
      </div>
    </template>

    <div class="mt-5 text-center text-sm">
      <router-link to="/login" class="text-muted-foreground hover:text-foreground hover:underline">{{ t('common.backToSignIn') }}</router-link>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { translateApiError } from '@/i18n'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import * as api from '@/api'

const { t } = useI18n()
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
    error.value = t('resetPassword.mismatch')
    return
  }
  loading.value = true
  try {
    await api.resetPassword(token.value!, newPassword.value)
    done.value = true
  } catch (e: unknown) {
    error.value = translateApiError(e, t('resetPassword.failed'))
  } finally {
    loading.value = false
  }
}
</script>
