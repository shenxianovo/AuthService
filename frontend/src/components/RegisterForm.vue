<template>
  <form @submit.prevent="$emit('submit')">
    <div class="form-group">
      <input type="text" :value="displayName" @input="$emit('update:displayName', ($event.target as HTMLInputElement).value)" placeholder="Display name" class="input" required />
    </div>
    <div class="form-group">
      <input type="email" :value="email" @input="$emit('update:email', ($event.target as HTMLInputElement).value)" placeholder="Email address" class="input" required />
    </div>
    <div class="form-group">
      <input type="password" :value="password" @input="$emit('update:password', ($event.target as HTMLInputElement).value)" placeholder="Password" class="input" required />
    </div>
    <button type="submit" class="btn btn-primary" :disabled="loading">
      {{ loading ? 'Creating account...' : 'Create account' }}
    </button>
  </form>
</template>

<script setup lang="ts">
defineProps<{
  displayName: string
  email: string
  password: string
  loading: boolean
}>()

defineEmits<{
  submit: []
  'update:displayName': [value: string]
  'update:email': [value: string]
  'update:password': [value: string]
}>()
</script>
