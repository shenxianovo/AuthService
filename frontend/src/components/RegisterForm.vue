<template>
  <form class="space-y-4" @submit.prevent="$emit('submit')">
    <div class="space-y-2">
      <Label for="register-username">{{ t('register.username') }}</Label>
      <Input
        id="register-username"
        type="text"
        :model-value="username"
        :placeholder="t('register.usernamePlaceholder')"
        autocomplete="username"
        required
        minlength="3"
        maxlength="39"
        pattern="[a-z0-9](?:-?[a-z0-9]){2,38}"
        :title="t('register.usernameTitle')"
        @update:model-value="$emit('update:username', String($event).toLowerCase())"
      />
    </div>
    <div class="space-y-2">
      <Label for="register-name">{{ t('register.displayName') }}</Label>
      <Input
        id="register-name"
        type="text"
        :model-value="displayName"
        :placeholder="t('register.displayNamePlaceholder')"
        autocomplete="name"
        required
        @update:model-value="$emit('update:displayName', String($event))"
      />
    </div>
    <div class="space-y-2">
      <Label for="register-email">{{ t('common.email') }}</Label>
      <Input
        id="register-email"
        type="email"
        :model-value="email"
        :placeholder="t('login.emailPlaceholder')"
        autocomplete="email"
        required
        @update:model-value="$emit('update:email', String($event))"
      />
    </div>
    <div class="space-y-2">
      <Label for="register-password">{{ t('common.password') }}</Label>
      <Input
        id="register-password"
        type="password"
        :model-value="password"
        placeholder="••••••••"
        autocomplete="new-password"
        required
        @update:model-value="$emit('update:password', String($event))"
      />
    </div>
    <Button type="submit" class="w-full" :disabled="loading">
      {{ loading ? t('register.creating') : t('register.create') }}
    </Button>
  </form>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const { t } = useI18n()

defineProps<{
  username: string
  displayName: string
  email: string
  password: string
  loading: boolean
}>()

defineEmits<{
  submit: []
  'update:username': [value: string]
  'update:displayName': [value: string]
  'update:email': [value: string]
  'update:password': [value: string]
}>()
</script>
