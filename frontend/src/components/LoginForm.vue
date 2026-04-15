<template>
  <form @submit.prevent="$emit('submit')">
    <div class="form-group">
      <input type="email" :value="email" @input="$emit('update:email', ($event.target as HTMLInputElement).value)" placeholder="Email address" class="input" required />
    </div>
    <div class="form-group">
      <input type="password" :value="password" @input="$emit('update:password', ($event.target as HTMLInputElement).value)" placeholder="Password" class="input" required />
    </div>
    <button type="submit" class="btn btn-primary" :disabled="loading">
      {{ loading ? 'Signing in...' : 'Sign in' }}
    </button>
  </form>
</template>

<script setup lang="ts">
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
