<template>
  <div>
    <h2 class="text-xl font-bold text-foreground mb-2">{{ t('forgotPassword.title') }}</h2>

    <template v-if="!submitted">
      <p class="text-sm text-muted-foreground mb-5">{{ t('forgotPassword.intro') }}</p>
      <form class="space-y-4" @submit.prevent="handleSubmit">
        <div class="space-y-2">
          <Label for="forgot-email">{{ t('common.email') }}</Label>
          <Input id="forgot-email" v-model="email" type="email" :placeholder="t('login.emailPlaceholder')" required />
        </div>
        <Button type="submit" class="w-full" :disabled="loading || !email">
          {{ loading ? t('forgotPassword.sending') : t('forgotPassword.send') }}
        </Button>
      </form>
    </template>

    <template v-else>
      <div class="px-3.5 py-3 rounded-md text-sm leading-relaxed bg-primary-soft text-primary-dark border border-primary/20">
        <i18n-t keypath="forgotPassword.sent" tag="span">
          <template #email><strong>{{ email }}</strong></template>
        </i18n-t>
      </div>
    </template>

    <div class="mt-5 text-center text-sm">
      <router-link to="/login" class="text-muted-foreground hover:text-foreground hover:underline">{{ t('common.backToSignIn') }}</router-link>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import * as api from '@/api'

const { t } = useI18n()

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
