<template>
  <form class="space-y-4" @submit.prevent="$emit('submit')">
    <div class="space-y-2">
      <Label for="register-username">Username</Label>
      <Input
        id="register-username"
        type="text"
        :model-value="username"
        placeholder="your-handle"
        autocomplete="username"
        required
        minlength="3"
        maxlength="39"
        pattern="[a-z0-9](?:-?[a-z0-9]){2,38}"
        title="3-39 characters: lowercase letters, digits and single hyphens (not at the ends)"
        @update:model-value="$emit('update:username', String($event).toLowerCase())"
      />
    </div>
    <div class="space-y-2">
      <Label for="register-name">Display name</Label>
      <Input
        id="register-name"
        type="text"
        :model-value="displayName"
        placeholder="Your name"
        autocomplete="name"
        required
        @update:model-value="$emit('update:displayName', String($event))"
      />
    </div>
    <div class="space-y-2">
      <Label for="register-email">Email</Label>
      <Input
        id="register-email"
        type="email"
        :model-value="email"
        placeholder="you@example.com"
        autocomplete="email"
        required
        @update:model-value="$emit('update:email', String($event))"
      />
    </div>
    <div class="space-y-2">
      <Label for="register-password">Password</Label>
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
      {{ loading ? 'Creating account...' : 'Create account' }}
    </Button>
  </form>
</template>

<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

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
