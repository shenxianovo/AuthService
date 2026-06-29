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

    <!-- Toolbar: create -->
    <div class="flex items-center justify-between gap-3">
      <div class="flex items-center gap-2">
        <span class="text-sm font-medium text-foreground">Your keys</span>
        <!-- Mobile sort selector (desktop sorts via column headers) -->
        <select
          v-if="keys.length"
          v-model="mobileSort"
          class="md:hidden h-8 rounded-md border border-input bg-transparent px-2 text-xs text-foreground"
          aria-label="Sort keys"
        >
          <option value="created-desc">Newest</option>
          <option value="created-asc">Oldest</option>
          <option value="name-asc">Name A–Z</option>
          <option value="name-desc">Name Z–A</option>
          <option value="lastUsed-desc">Recently used</option>
          <option value="status-asc">Status</option>
        </select>
      </div>
      <div v-if="creating" class="flex items-center gap-2">
        <Input
          ref="nameInput"
          v-model="newKeyName"
          type="text"
          placeholder="Key name (e.g. Heartbeat Agent)"
          class="h-8 w-56"
          :disabled="loading"
          maxlength="100"
          @keyup.enter="handleCreate"
          @keyup.escape="cancelCreate"
        />
        <Button size="sm" :disabled="loading || !newKeyName.trim()" @click="handleCreate">Create</Button>
        <Button variant="ghost" size="sm" :disabled="loading" @click="cancelCreate">Cancel</Button>
      </div>
      <Button v-else size="sm" @click="startCreate">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
        New key
      </Button>
    </div>

    <!-- Empty state -->
    <p v-if="!keys.length" class="text-sm text-muted-foreground py-4 text-center">No API keys yet</p>

    <!-- Table (md+) -->
    <div v-else class="hidden md:block">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>
              <button class="inline-flex items-center gap-1 hover:text-foreground transition-colors" @click="toggleSort('name')">
                Name <span class="text-xs">{{ sortIndicator('name') }}</span>
              </button>
            </TableHead>
            <TableHead>Key</TableHead>
            <TableHead>
              <button class="inline-flex items-center gap-1 hover:text-foreground transition-colors" @click="toggleSort('created')">
                Created <span class="text-xs">{{ sortIndicator('created') }}</span>
              </button>
            </TableHead>
            <TableHead>
              <button class="inline-flex items-center gap-1 hover:text-foreground transition-colors" @click="toggleSort('lastUsed')">
                Last used <span class="text-xs">{{ sortIndicator('lastUsed') }}</span>
              </button>
            </TableHead>
            <TableHead>
              <button class="inline-flex items-center gap-1 hover:text-foreground transition-colors" @click="toggleSort('status')">
                Status <span class="text-xs">{{ sortIndicator('status') }}</span>
              </button>
            </TableHead>
            <TableHead class="w-px"></TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="key in sortedKeys" :key="key.id" :class="key.isRevoked && 'opacity-55'">
            <TableCell class="font-medium text-foreground">{{ key.name }}</TableCell>
            <TableCell>
              <code class="text-xs text-muted-foreground font-mono bg-secondary px-2 py-0.5 rounded">{{ key.prefix }}</code>
            </TableCell>
            <TableCell class="text-muted-foreground">{{ formatDate(key.createdAt) }}</TableCell>
            <TableCell class="text-muted-foreground">{{ key.lastUsedAt ? formatRelative(key.lastUsedAt) : 'Never' }}</TableCell>
            <TableCell>
              <Badge :variant="key.isRevoked ? 'destructive' : 'default'">{{ key.isRevoked ? 'Revoked' : 'Active' }}</Badge>
            </TableCell>
            <TableCell class="text-right">
              <button
                v-if="!key.isRevoked"
                class="inline-flex items-center justify-center size-[22px] rounded-full text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors disabled:opacity-40"
                :disabled="loading"
                title="Revoke key"
                @click="handleRevoke(key.id!, key.name!)"
              >&#x2715;</button>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>

    <!-- Card stack (mobile) -->
    <div v-if="keys.length" class="md:hidden flex flex-col gap-2">
      <div
        v-for="key in sortedKeys"
        :key="key.id"
        :class="cn('rounded-lg bg-muted p-3.5 space-y-2', key.isRevoked && 'opacity-55')"
      >
        <div class="flex items-center justify-between gap-2">
          <span class="text-sm font-medium text-foreground">{{ key.name }}</span>
          <div class="flex items-center gap-2">
            <Badge :variant="key.isRevoked ? 'destructive' : 'default'">{{ key.isRevoked ? 'Revoked' : 'Active' }}</Badge>
            <button
              v-if="!key.isRevoked"
              class="inline-flex items-center justify-center size-[22px] rounded-full text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors disabled:opacity-40"
              :disabled="loading"
              title="Revoke key"
              @click="handleRevoke(key.id!, key.name!)"
            >&#x2715;</button>
          </div>
        </div>
        <code class="block text-xs text-muted-foreground font-mono bg-secondary px-2 py-0.5 rounded w-fit">{{ key.prefix }}</code>
        <div class="text-xs text-muted-foreground">
          Created {{ formatDate(key.createdAt) }} · {{ key.lastUsedAt ? `Last used ${formatRelative(key.lastUsedAt)}` : 'Never used' }}
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted } from 'vue'
import type { ComponentPublicInstance } from 'vue'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
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
const creating = ref(false)
const nameInput = ref<ComponentPublicInstance | null>(null)

// --- sorting ---
type SortKey = 'name' | 'created' | 'lastUsed' | 'status'
type SortDir = 'asc' | 'desc'
const sortKey = ref<SortKey>('created')
const sortDir = ref<SortDir>('desc')

function toggleSort(key: SortKey) {
  if (sortKey.value === key) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortKey.value = key
    // sensible default direction per column
    sortDir.value = key === 'name' ? 'asc' : 'desc'
  }
}

function sortIndicator(key: SortKey): string {
  if (sortKey.value !== key) return '↕'
  return sortDir.value === 'asc' ? '↑' : '↓'
}

// Two-way bridge for the mobile <select> ("field-dir")
const mobileSort = computed({
  get: () => `${sortKey.value}-${sortDir.value}`,
  set: (val: string) => {
    const [k, d] = val.split('-') as [SortKey, SortDir]
    sortKey.value = k
    sortDir.value = d
  },
})

function timeVal(date: unknown): number {
  if (!date) return 0
  const d = date instanceof Date ? date : new Date(date as string)
  const t = d.getTime()
  return Number.isNaN(t) ? 0 : t
}

const sortedKeys = computed(() => {
  const dir = sortDir.value === 'asc' ? 1 : -1
  return [...keys.value].sort((a, b) => {
    // Last-used: never-used keys always sink to the bottom, both directions
    if (sortKey.value === 'lastUsed') {
      const ta = timeVal(a.lastUsedAt)
      const tb = timeVal(b.lastUsedAt)
      if (ta === 0 && tb === 0) return 0
      if (ta === 0) return 1
      if (tb === 0) return -1
      return (ta - tb) * dir
    }
    let cmp = 0
    switch (sortKey.value) {
      case 'name':
        cmp = (a.name ?? '').localeCompare(b.name ?? '')
        break
      case 'created':
        cmp = timeVal(a.createdAt) - timeVal(b.createdAt)
        break
      case 'status':
        cmp = Number(a.isRevoked ?? false) - Number(b.isRevoked ?? false)
        break
    }
    return cmp * dir
  })
})

async function loadKeys() {
  try {
    keys.value = await api.listApiKeys()
  } catch (e: unknown) {
    emit('error', e instanceof Error ? e.message : 'Failed to load API keys')
  }
}

function startCreate() {
  creating.value = true
  newKeyName.value = ''
  nextTick(() => (nameInput.value?.$el as HTMLInputElement | undefined)?.focus())
}

function cancelCreate() {
  creating.value = false
  newKeyName.value = ''
}

async function handleCreate() {
  if (!newKeyName.value.trim()) return
  emit('update:loading', true)
  try {
    const result: CreateApiKeyResponse = await api.createApiKey(newKeyName.value.trim())
    newlyCreatedKey.value = result.key!
    copied.value = false
    newKeyName.value = ''
    creating.value = false
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

function formatRelative(date: unknown): string {
  const d = date instanceof Date ? date : new Date(date as string)
  const diffMs = Date.now() - d.getTime()
  const sec = Math.floor(diffMs / 1000)
  const min = Math.floor(sec / 60)
  const hr = Math.floor(min / 60)
  const day = Math.floor(hr / 24)
  if (sec < 60) return 'Just now'
  if (min < 60) return `${min}m ago`
  if (hr < 24) return `${hr}h ago`
  if (day < 30) return `${day}d ago`
  return formatDate(d)
}

onMounted(loadKeys)
</script>
