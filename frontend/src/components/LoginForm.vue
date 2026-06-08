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

<style scoped>
.form-group { margin-bottom: 14px; }
.input { width: 100%; padding: 12px 14px; border: 1.5px solid #e0e0e0; border-radius: 10px; font-size: 14px; transition: border-color .2s; outline: none; background: #fafafa; }
.input:focus { border-color: #333; background: #fff; }
.btn { width: 100%; padding: 12px; border: none; border-radius: 10px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all .2s; }
.btn-primary { background: #1a1a2e; color: #fff; }
.btn-primary:hover:not(:disabled) { background: #2d2d44; box-shadow: 0 4px 12px rgba(0,0,0,.15); transform: translateY(-1px); }
.btn:disabled { opacity: .6; cursor: not-allowed; }
</style>
