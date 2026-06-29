<template>
  <div class="space-y-4">
    <!-- Newly created key alert -->
    <div v-if="newlyCreatedKey" class="rounded-lg border border-amber-300 bg-amber-50 p-4 space-y-2.5">
      <div class="flex items-center gap-2 text-sm text-amber-800">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
        <strong>Copy your API key now — it won't be shown again!</strong>
      </div>
      <div
        class="flex items-center justify-between gap-3 rounded-md bg-slate-900 px-3.5 py-3 font-mono text-xs text-emerald-400 cursor-pointer hover:bg-slate-800 transition-colors break-all"
        @click="copyKey"
      >
        <code class="flex-1 min-w-0">{{ newlyCreatedKey }}</code>
        <span class="text-[11px] text-slate-400 whitespace-nowrap shrink-0">{{ copied ? '✓ Copied' : 'Click to copy' }}</span>
      </div>
      <Button variant="outline" size="sm" @click="newlyCreatedKey = null">Dismiss</Button>
    </div>

    <!-- Key list -->
    <div v-if="keys.length" class="flex flex-col gap-2">
      <div
        v-for="key in keys"
        :key="key.id"
        :class="cn(
          'flex items-center justify-between gap-3 flex-wrap px-3.5 py-2.5 rounded-lg bg-muted',
          key.isRevoked && 'opacity-55',
        )"
      >
        <div class="flex items-center gap-2.5 flex-1 min-w-0">
          <span class="text-sm font-medium text-foreground">{{ key.name }}</span>
          <code class="text-xs text-muted-foreground font-mono bg-secondary px-2 py-0.5 rounded">{{ key.prefix }}</code>
        </div>
        <div class="flex items-center gap-1.5 flex-wrap">
          <span class="text-xs text-muted-foreground">Created {{ formatDate(key.createdAt) }}</span>
          <span v-if="key.lastUsedAt" class="text-xs text-muted-foreground">· Last used {{ formatDate(key.lastUsedAt) }}</span>
          <Badge v-if="key.isRevoked" variant="destructive">Revoked</Badge>
        </div>
        <button
          v-if="!key.isRevoked"
          class="inline-flex items-center justify-center size-[22px] rounded-full text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors disabled:opacity-40"
          :disabled="loading"
          title="Revoke key"
          @click="handleRevoke(key.id!, key.name!)"
        >&#x2715;</button>
      </div>
    </div>
    <p v-else class="text-sm text-muted-foreground">No API keys yet</p>

    <!-- Create new key -->
    <div class="flex gap-2 items-center">
      <Input
        v-model="newKeyName"
        type="text"
        placeholder="Key name (e.g. Heartbeat Agent)"
        class="flex-1"
        :disabled="loading"
        maxlength="100"
        @keyup.enter="handleCreate"
      />
      <Button size="sm" :disabled="loading || !newKeyName.trim()" @click="handleCreate">Create</Button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
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
