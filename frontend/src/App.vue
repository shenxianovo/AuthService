<template>
  <div class="page">
    <AuthCard />
  </div>
</template>

<script setup lang="ts">
import AuthCard from '@/components/AuthCard.vue'
</script>

<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
.page { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #f0f1f3; padding: 20px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
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
.input { width: 100%; padding: 12px 14px; border: 1.5px solid #e0e0e0; border-radius: 10px; font-size: 14px; transition: border-color .2s; outline: none; background: #fafafa; }
.input:focus { border-color: #333; background: #fff; }
.btn { width: 100%; padding: 12px; border: none; border-radius: 10px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all .2s; display: flex; align-items: center; justify-content: center; gap: 10px; }
.btn:disabled { opacity: .6; cursor: not-allowed; }
.btn-primary { background: #1a1a2e; color: #fff; }
.btn-primary:hover:not(:disabled) { background: #2d2d44; box-shadow: 0 4px 12px rgba(0,0,0,.15); transform: translateY(-1px); }
.btn-secondary { background: #1a1a2e; color: #fff; width: auto; padding: 10px 20px; white-space: nowrap; flex-shrink: 0; }
.btn-danger { background: transparent; color: #dc3545; border: 1.5px solid #dc3545; margin-top: 16px; }
.btn-danger:hover { background: #dc3545; color: #fff; }
.btn-github { background: #24292e; color: #fff; }
.btn-github:hover:not(:disabled) { background: #1b1f23; }
.btn-google { background: #fff; color: #333; border: 1.5px solid #e0e0e0; }
.btn-google:hover:not(:disabled) { background: #f8f8f8; border-color: #ccc; }
.icon { width: 18px; height: 18px; flex-shrink: 0; }
.oauth-buttons { display: flex; flex-direction: column; gap: 10px; }
.oauth-divider { display: flex; align-items: center; margin: 20px 0; color: #aaa; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; }
.oauth-divider::before, .oauth-divider::after { content: ""; flex: 1; height: 1px; background: #e8e8e8; }
.oauth-divider span { padding: 0 14px; }
.message { margin-top: 16px; padding: 10px 14px; border-radius: 8px; font-size: 13px; text-align: center; }
.message.error { background: #fff0f0; color: #dc3545; border: 1px solid #ffcdd2; }
.message.success { background: #f0fff4; color: #28a745; border: 1px solid #c8e6c9; }
.profile-section { display: flex; align-items: center; gap: 16px; margin-bottom: 24px; padding-bottom: 20px; border-bottom: 1px solid #f0f0f0; }
.avatar { width: 52px; height: 52px; border-radius: 50%; background: #1a1a2e; color: #fff; display: flex; align-items: center; justify-content: center; font-size: 22px; font-weight: 700; flex-shrink: 0; }
.profile-info h2 { font-size: 18px; font-weight: 700; color: #1a1a2e; }
.user-id { font-size: 11px; color: #aaa; font-family: 'SF Mono', Monaco, 'Cascadia Code', monospace; word-break: break-all; }
.details-section { margin-bottom: 24px; }
.detail-group { margin-bottom: 16px; }
.detail-label { font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: .5px; color: #999; margin-bottom: 6px; }
.detail-item { display: flex; align-items: center; justify-content: space-between; padding: 6px 0; font-size: 14px; color: #333; }
.badge-group { display: flex; gap: 4px; }
.badge { padding: 2px 8px; border-radius: 20px; font-size: 10px; font-weight: 600; text-transform: uppercase; letter-spacing: .3px; }
.badge-primary { background: #eef0f2; color: #555; }
.badge-success { background: #e8f5e9; color: #4caf50; }
.provider-list { display: flex; gap: 8px; flex-wrap: wrap; }
.provider-chip { display: flex; align-items: center; gap: 6px; padding: 6px 10px 6px 12px; background: #f5f5f7; border-radius: 20px; font-size: 13px; font-weight: 500; color: #333; }
.btn-unlink { display: flex; align-items: center; justify-content: center; width: 16px; height: 16px; padding: 0; margin-left: 2px; border: none; background: transparent; border-radius: 50%; color: #aaa; font-size: 11px; cursor: pointer; line-height: 1; transition: background .15s, color .15s; }
.btn-unlink:hover:not(:disabled) { background: #ff5555; color: #fff; }
.btn-unlink:disabled { opacity: .4; cursor: not-allowed; }
.provider-icon { width: 8px; height: 8px; border-radius: 50%; }
.provider-icon.github { background: #24292e; }
.provider-icon.google { background: #4285f4; }
.status-set { color: #4caf50; font-weight: 500; }
.status-unset { color: #999; }
.muted { color: #aaa; font-size: 13px; }
.settings-section { border-top: 1px solid #f0f0f0; padding-top: 20px; margin-bottom: 8px; }
.settings-section h3 { font-size: 15px; font-weight: 600; color: #1a1a2e; margin-bottom: 16px; }
.input-row { display: flex; gap: 10px; margin-bottom: 16px; }
.input-row .input { flex: 1; }
</style>