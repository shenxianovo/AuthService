<template>
  <div class="auth-page">
    <div class="auth-card">
      <div class="header">
        <h1>AuthService</h1>
        <p v-if="externalRedirect" class="subtitle">
          Sign in to continue to <strong>{{ externalRedirectHost }}</strong>
        </p>
        <p v-else class="subtitle">Sign in to your account</p>
      </div>
      <div class="auth-content">
        <router-view />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useExternalRedirect } from '@/stores/externalRedirect'

const { externalRedirect } = useExternalRedirect()

const externalRedirectHost = computed(() => {
  try { return externalRedirect.value ? new URL(externalRedirect.value).host : '' }
  catch { return externalRedirect.value ?? '' }
})
</script>

<style scoped>
.auth-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f0f1f3;
  padding: 20px;
}

.auth-card {
  width: 100%;
  max-width: 420px;
  background: #fff;
  border-radius: 16px;
  box-shadow: 0 4px 24px rgba(0,0,0,.08);
  overflow: hidden;
}

.header {
  text-align: center;
  padding: 32px 32px 0;
}

.header h1 {
  font-size: 22px;
  font-weight: 700;
  color: #1a1a2e;
  margin-bottom: 4px;
}

.subtitle {
  font-size: 13px;
  color: #888;
  margin-top: 4px;
}

.subtitle strong {
  color: #333;
}

.auth-content {
  padding: 24px 32px 32px;
}
</style>
