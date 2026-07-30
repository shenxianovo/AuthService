<template>
  <div class="space-y-4">
    <!-- Newly created key alert -->
    <div v-if="newlyCreatedKey" class="rounded-lg border border-amber-300 bg-amber-50 p-4 space-y-2.5">
      <div class="flex items-center gap-2 text-sm text-amber-800">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
        <strong>{{ t('apiKeys.copyNow') }}</strong>
      </div>
      <div
        class="flex items-center justify-between gap-3 rounded-md bg-slate-900 px-3.5 py-3 font-mono text-xs text-emerald-400 cursor-pointer hover:bg-slate-800 transition-colors break-all"
        @click="copyKey"
      >
        <code class="flex-1 min-w-0">{{ newlyCreatedKey }}</code>
        <span class="text-[11px] text-slate-400 whitespace-nowrap shrink-0">{{ copied ? t('apiKeys.copied') : t('apiKeys.clickToCopy') }}</span>
      </div>
      <Button variant="outline" size="sm" @click="newlyCreatedKey = null">{{ t('apiKeys.dismiss') }}</Button>
    </div>

    <!-- Toolbar: create -->
    <div class="flex items-center justify-between gap-3">
      <div class="flex items-center gap-2">
        <span class="text-sm font-medium text-foreground">{{ t('apiKeys.yourKeys') }}</span>
        <!-- Mobile sort selector (desktop sorts via column headers) -->
        <select
          v-if="keys.length"
          v-model="mobileSort"
          class="md:hidden h-8 rounded-md border border-input bg-transparent px-2 text-xs text-foreground"
          :aria-label="t('apiKeys.sortLabel')"
        >
          <option value="created-desc">{{ t('apiKeys.sortNewest') }}</option>
          <option value="created-asc">{{ t('apiKeys.sortOldest') }}</option>
          <option value="name-asc">{{ t('apiKeys.sortNameAsc') }}</option>
          <option value="name-desc">{{ t('apiKeys.sortNameDesc') }}</option>
          <option value="lastUsed-desc">{{ t('apiKeys.sortRecentlyUsed') }}</option>
          <option value="status-asc">{{ t('apiKeys.sortStatus') }}</option>
        </select>
      </div>
      <div v-if="creating" class="flex items-center gap-2">
        <Input
          ref="nameInput"
          v-model="newKeyName"
          type="text"
          :placeholder="t('apiKeys.keyNamePlaceholder')"
          class="h-8 w-56"
          :disabled="loading"
          maxlength="100"
          @keyup.enter="handleCreate"
          @keyup.escape="cancelCreate"
        />
        <Button size="sm" :disabled="loading || !newKeyName.trim()" @click="handleCreate">{{ t('apiKeys.create') }}</Button>
        <Button variant="ghost" size="sm" :disabled="loading" @click="cancelCreate">{{ t('apiKeys.cancel') }}</Button>
      </div>
      <Button v-else size="sm" @click="startCreate">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
        {{ t('apiKeys.newKey') }}
      </Button>
    </div>

    <!-- Empty state -->
    <p v-if="!keys.length" class="text-sm text-muted-foreground py-4 text-center">{{ t('apiKeys.none') }}</p>

    <!-- Table (md+) -->
    <div v-else class="hidden md:block">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>
              <button class="inline-flex items-center gap-1 hover:text-foreground transition-colors" @click="toggleSort('name')">
                {{ t('apiKeys.colName') }} <span class="text-xs">{{ sortIndicator('name') }}</span>
              </button>
            </TableHead>
            <TableHead>{{ t('apiKeys.colKey') }}</TableHead>
            <TableHead>
              <button class="inline-flex items-center gap-1 hover:text-foreground transition-colors" @click="toggleSort('created')">
                {{ t('apiKeys.colCreated') }} <span class="text-xs">{{ sortIndicator('created') }}</span>
              </button>
            </TableHead>
            <TableHead>
              <button class="inline-flex items-center gap-1 hover:text-foreground transition-colors" @click="toggleSort('lastUsed')">
                {{ t('apiKeys.colLastUsed') }} <span class="text-xs">{{ sortIndicator('lastUsed') }}</span>
              </button>
            </TableHead>
            <TableHead>
              <button class="inline-flex items-center gap-1 hover:text-foreground transition-colors" @click="toggleSort('status')">
                {{ t('apiKeys.colStatus') }} <span class="text-xs">{{ sortIndicator('status') }}</span>
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
            <TableCell class="text-muted-foreground">{{ key.lastUsedAt ? formatRelative(key.lastUsedAt) : t('apiKeys.never') }}</TableCell>
            <TableCell>
              <Badge :variant="key.isRevoked ? 'destructive' : 'default'">{{ key.isRevoked ? t('apiKeys.revoked') : t('apiKeys.active') }}</Badge>
            </TableCell>
            <TableCell class="text-right">
              <button
                v-if="!key.isRevoked"
                class="inline-flex items-center justify-center size-[22px] rounded-full text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors disabled:opacity-40"
                :disabled="loading"
                :title="t('apiKeys.revokeTitle')"
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
            <Badge :variant="key.isRevoked ? 'destructive' : 'default'">{{ key.isRevoked ? t('apiKeys.revoked') : t('apiKeys.active') }}</Badge>
            <button
              v-if="!key.isRevoked"
              class="inline-flex items-center justify-center size-[22px] rounded-full text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors disabled:opacity-40"
              :disabled="loading"
              :title="t('apiKeys.revokeTitle')"
              @click="handleRevoke(key.id!, key.name!)"
            >&#x2715;</button>
          </div>
        </div>
        <code class="block text-xs text-muted-foreground font-mono bg-secondary px-2 py-0.5 rounded w-fit">{{ key.prefix }}</code>
        <div class="text-xs text-muted-foreground">
          {{ t('apiKeys.createdOn', { date: formatDate(key.createdAt) }) }} · {{ key.lastUsedAt ? t('apiKeys.lastUsedRel', { rel: formatRelative(key.lastUsedAt) }) : t('apiKeys.neverUsed') }}
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { translateApiError } from '@/i18n'
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

const { t, locale } = useI18n()

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
    emit('error', translateApiError(e, t('apiKeys.loadFailed')))
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
    emit('success', t('apiKeys.createdMsg', { name: result.name }))
  } catch (e: unknown) {
    emit('error', translateApiError(e, t('apiKeys.createFailed')))
  } finally {
    emit('update:loading', false)
  }
}

async function handleRevoke(id: string, name: string) {
  if (!confirm(t('apiKeys.confirmRevoke', { name }))) return
  emit('update:loading', true)
  try {
    await api.revokeApiKey(id)
    await loadKeys()
    emit('success', t('apiKeys.revokedMsg', { name }))
  } catch (e: unknown) {
    emit('error', translateApiError(e, t('apiKeys.revokeFailed')))
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
  return d.toLocaleDateString(locale.value, { month: 'short', day: 'numeric', year: 'numeric' })
}

function formatRelative(date: unknown): string {
  const d = date instanceof Date ? date : new Date(date as string)
  const diffMs = Date.now() - d.getTime()
  const sec = Math.floor(diffMs / 1000)
  const min = Math.floor(sec / 60)
  const hr = Math.floor(min / 60)
  const day = Math.floor(hr / 24)
  if (sec < 60) return t('apiKeys.justNow')
  if (min < 60) return t('apiKeys.minutesAgo', { n: min })
  if (hr < 24) return t('apiKeys.hoursAgo', { n: hr })
  if (day < 30) return t('apiKeys.daysAgo', { n: day })
  return formatDate(d)
}

onMounted(loadKeys)
</script>
