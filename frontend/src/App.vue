<template>
  <div :class="isDashboard ? 'page-dashboard' : 'page'">
    <AuthCard />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import AuthCard from '@/components/AuthCard.vue'
import { authStore } from '@/stores/auth'

const isDashboard = computed(() => authStore.appState.value === 'profile')
</script>

<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }

/* Centered layout for login/register/email-verify */
.page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f0f1f3;
  padding: 20px;
}

/* Full-width layout for dashboard */
.page-dashboard {
  min-height: 100vh;
  background: #f4f6f9;
}

/* Auth card (login/register only) */
.auth-card { width: 100%; max-width: 420px; background: #fff; border-radius: 16px; box-shadow: 0 4px 24px rgba(0,0,0,.08); overflow: hidden; }
.header { text-align: center; padding: 32px 32px 0; }
.header h1 { font-size: 22px; font-weight: 700; color: #1a1a2e; margin-bottom: 4px; }
.subtitle { font-size: 13px; color: #888; margin-top: 4px; }
.subtitle strong { color: #333; }
.auth-content { padding: 24px 32px 32px; }
.tabs { display: flex; margin-bottom: 24px; background: #f5f5f7; border-radius: 10px; padding: 3px; }
.tabs button { flex: 1; padding: 10px; border: none; background: transparent; border-radius: 8px; font-size: 14px; font-weight: 500; color: #888; cursor: pointer; transition: all .2s; }
.tabs button.active { background: #fff; color: #1a1a2e; box-shadow: 0 1px 3px rgba(0,0,0,.1); font-weight: 600; }
.form-group { margin-bottom: 14px; }
.auth-card .input { width: 100%; padding: 12px 14px; border: 1.5px solid #e0e0e0; border-radius: 10px; font-size: 14px; transition: border-color .2s; outline: none; background: #fafafa; }
.auth-card .input:focus { border-color: #333; background: #fff; }
.auth-card .btn { width: 100%; padding: 12px; border: none; border-radius: 10px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all .2s; display: flex; align-items: center; justify-content: center; gap: 10px; }
.auth-card .btn:disabled { opacity: .6; cursor: not-allowed; }
.auth-card .btn-primary { background: #1a1a2e; color: #fff; }
.auth-card .btn-primary:hover:not(:disabled) { background: #2d2d44; box-shadow: 0 4px 12px rgba(0,0,0,.15); transform: translateY(-1px); }
.auth-card .btn-github { background: #24292e; color: #fff; }
.auth-card .btn-github:hover:not(:disabled) { background: #1b1f23; }
.auth-card .btn-google { background: #fff; color: #333; border: 1.5px solid #e0e0e0; }
.auth-card .btn-google:hover:not(:disabled) { background: #f8f8f8; border-color: #ccc; }
.icon { width: 18px; height: 18px; flex-shrink: 0; }
.oauth-buttons { display: flex; flex-direction: column; gap: 10px; }
.oauth-divider { display: flex; align-items: center; margin: 20px 0; color: #aaa; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; }
.oauth-divider::before, .oauth-divider::after { content: ""; flex: 1; height: 1px; background: #e8e8e8; }
.oauth-divider span { padding: 0 14px; }
.message { margin-top: 16px; padding: 10px 14px; border-radius: 8px; font-size: 13px; text-align: center; }
.message.error { background: #fff0f0; color: #dc3545; border: 1px solid #ffcdd2; }
.message.success { background: #f0fff4; color: #28a745; border: 1px solid #c8e6c9; }
</style>
