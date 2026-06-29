<template>
  <form class="space-y-4" @submit.prevent="$emit('submit')">
    <div class="space-y-2">
      <Label for="login-email">Email</Label>
      <Input
        id="login-email"
        type="email"
        :model-value="email"
        placeholder="you@example.com"
        autocomplete="email"
        required
        @update:model-value="$emit('update:email', String($event))"
      />
    </div>
    <div class="space-y-2">
      <Label for="login-password">Password</Label>
      <Input
        id="login-password"
        type="password"
        :model-value="password"
        placeholder="••••••••"
        autocomplete="current-password"
        required
        @update:model-value="$emit('update:password', String($event))"
      />
    </div>
    <Button type="submit" class="w-full" :disabled="loading">
      {{ loading ? 'Signing in...' : 'Sign in' }}
    </Button>
  </form>
</template>

<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

defineProps<{
  email: string
  password: string
  loading: boolean
}>()

defineEmits<{
  submit: []
  'update:email': [value: string]
  'update:password': [value: string]
}>()
</script>
