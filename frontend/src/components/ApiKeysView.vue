<template>
  <div>
    <!-- Newly created key alert -->
    <div v-if="newlyCreatedKey" class="new-key-alert">
      <div class="new-key-header">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
        <strong>Copy your API key now — it won't be shown again!</strong>
      </div>
      <div class="new-key-value" @click="copyKey">
        <code>{{ newlyCreatedKey }}</code>
        <span class="copy-hint">{{ copied ? '✓ Copied' : 'Click to copy' }}</span>
      </div>
      <button class="btn-dismiss" @click="newlyCreatedKey = null">Dismiss</button>
    </div>

    <!-- Key list -->
    <div v-if="keys.length" class="key-list">
      <div v-for="key in keys" :key="key.id" class="list-item" :class="{ revoked: key.isRevoked }">
        <div class="key-info">
          <span class="key-name">{{ key.name }}</span>
          <code class="key-prefix">{{ key.prefix }}</code>
        </div>
        <div class="key-meta">
          <span class="key-date">Created {{ formatDate(key.createdAt) }}</span>
          <span v-if="key.lastUsedAt" class="key-date">· Last used {{ formatDate(key.lastUsedAt) }}</span>
          <span v-if="key.isRevoked" class="badge badge-revoked">Revoked</span>
        </div>
        <button
          v-if="!key.isRevoked"
          class="btn-icon-sm btn-remove"
          @click="handleRevoke(key.id!, key.name!)"
          :disabled="loading"
          title="Revoke key"
        >✕</button>
      </div>
    </div>
    <p v-else class="empty-state">No API keys yet</p>

    <!-- Create new key -->
    <div class="form-row">
      <input
        type="text"
        v-model="newKeyName"
        placeholder="Key name (e.g. Heartbeat Agent)"
        class="input"
        @keyup.enter="handleCreate"
        :disabled="loading"
        maxlength="100"
      />
      <button
        class="btn btn-sm"
        @click="handleCreate"
        :disabled="loading || !newKeyName.trim()"
      >Create</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import * as api from '@/api'
import type { ApiKeyListItem, CreateApiKeyResponse } from '@/api'

defineProps<{
  loading: boolean
}>()

const emit = defineEmits<{
  error: [message: string]
  success: [message: string]
  'update:loading': [value: boolean]
}>()

const keys = ref<ApiKeyListItem[]>([])
const newKeyName = ref('')
const newlyCreatedKey = ref<string | null>(null)
const copied = ref(false)

async function loadKeys() {
  try {
    keys.value = await api.listApiKeys()
  } catch (e: unknown) {
    emit('error', e instanceof Error ? e.message : 'Failed to load API keys')
  }
}

async function handleCreate() {
  if (!newKeyName.value.trim()) return
  emit('update:loading', true)
  try {
    const result: CreateApiKeyResponse = await api.createApiKey(newKeyName.value.trim())
    newlyCreatedKey.value = result.key!
    copied.value = false
    newKeyName.value = ''
    await loadKeys()
    emit('success', `API key "${result.name}" created`)
  } catch (e: unknown) {
    emit('error', e instanceof Error ? e.message : 'Failed to create API key')
  } finally {
    emit('update:loading', false)
  }
}

async function handleRevoke(id: string, name: string) {
  if (!confirm(`Revoke API key "${name}"? This cannot be undone.`)) return
  emit('update:loading', true)
  try {
    await api.revokeApiKey(id)
    await loadKeys()
    emit('success', `API key "${name}" revoked`)
  } catch (e: unknown) {
    emit('error', e instanceof Error ? e.message : 'Failed to revoke API key')
  } finally {
    emit('update:loading', false)
  }
}

async function copyKey() {
  if (!newlyCreatedKey.value) return
  try {
    await navigator.clipboard.writeText(newlyCreatedKey.value)
    copied.value = true
  } catch {
    // Fallback
  }
}

function formatDate(date: unknown): string {
  const d = date instanceof Date ? date : new Date(date as string)
  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

onMounted(loadKeys)
</script>

<style scoped>
.new-key-alert {
  background: #fffbeb;
  border: 1px solid #fbbf24;
  border-radius: 10px;
  padding: 14px 16px;
  margin-bottom: 16px;
}

.new-key-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #92400e;
  margin-bottom: 10px;
}

.new-key-value {
  background: #1a1a2e;
  color: #4ade80;
  padding: 12px 14px;
  border-radius: 8px;
  font-size: 12px;
  font-family: 'SF Mono', Monaco, 'Cascadia Code', monospace;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  word-break: break-all;
  transition: background .15s;
}

.new-key-value:hover {
  background: #2d2d44;
}

.new-key-value code {
  flex: 1;
  min-width: 0;
}

.copy-hint {
  font-size: 11px;
  color: #94a3b8;
  white-space: nowrap;
  flex-shrink: 0;
}

.btn-dismiss {
  margin-top: 10px;
  padding: 4px 12px;
  border: 1px solid #e0e0e0;
  background: #fff;
  border-radius: 6px;
  font-size: 12px;
  color: #666;
  cursor: pointer;
  transition: all .15s;
}

.btn-dismiss:hover {
  background: #f5f5f5;
  border-color: #ccc;
}

.key-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 16px;
}

.revoked {
  opacity: 0.55;
}

.key-info {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
  min-width: 0;
}

.key-name {
  font-size: 14px;
  font-weight: 500;
  color: #333;
}

.key-prefix {
  font-size: 12px;
  color: #888;
  font-family: 'SF Mono', Monaco, 'Cascadia Code', monospace;
  background: #eef0f2;
  padding: 2px 8px;
  border-radius: 4px;
}

.key-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.key-date {
  font-size: 12px;
  color: #999;
}

@media (max-width: 600px) {
  .new-key-value {
    flex-direction: column;
    gap: 8px;
  }
}
</style>