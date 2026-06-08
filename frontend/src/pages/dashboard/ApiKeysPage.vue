<template>
  <div class="page">
    <h1 class="page-title">API Keys</h1>

    <div class="card">
      <ApiKeysView
        :loading="loading"
        @error="(msg) => { error = msg }"
        @success="(msg) => { success = msg }"
        @update:loading="(val) => { loading = val }"
      />
    </div>

    <div v-if="error" class="toast error">{{ error }}</div>
    <div v-if="success" class="toast success">{{ success }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import ApiKeysView from '@/components/ApiKeysView.vue'

const loading = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)
</script>

<style scoped>
.page-title {
  font-size: 22px;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 24px;
}

.card {
  background: #fff;
  border-radius: 14px;
  padding: 24px;
  box-shadow: 0 2px 12px rgba(0,0,0,.05);
}

.toast {
  position: fixed;
  bottom: 24px;
  left: 50%;
  transform: translateX(-50%);
  padding: 12px 24px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  z-index: 1000;
  box-shadow: 0 8px 32px rgba(0,0,0,.12);
}
.toast.error { background: #fef2f2; color: #dc2626; border: 1px solid #fecaca; }
.toast.success { background: #f0fdf4; color: #16a34a; border: 1px solid #bbf7d0; }
</style>
