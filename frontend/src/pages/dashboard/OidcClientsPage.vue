<template>
  <div>
    <PageHeader title="OIDC Clients" description="Register and manage applications that sign in through this service." />

    <Card>
      <CardContent>
        <div class="space-y-4">
          <!-- Newly issued secret alert (shown once, mirrors the API key UX) -->
          <div v-if="issuedSecret" class="rounded-lg border border-amber-300 bg-amber-50 p-4 space-y-2.5">
            <div class="flex items-center gap-2 text-sm text-amber-800">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
              <strong>Copy the client secret for "{{ issuedSecretClientId }}" now — it won't be shown again!</strong>
            </div>
            <div
              class="flex items-center justify-between gap-3 rounded-md bg-slate-900 px-3.5 py-3 font-mono text-xs text-emerald-400 cursor-pointer hover:bg-slate-800 transition-colors break-all"
              @click="copySecret"
            >
              <code class="flex-1 min-w-0">{{ issuedSecret }}</code>
              <span class="text-[11px] text-slate-400 whitespace-nowrap shrink-0">{{ copied ? '✓ Copied' : 'Click to copy' }}</span>
            </div>
            <Button variant="outline" size="sm" @click="issuedSecret = null">Dismiss</Button>
          </div>

          <!-- Toolbar -->
          <div class="flex items-center justify-between gap-3">
            <span class="text-sm font-medium text-foreground">Registered clients</span>
            <Button v-if="!formOpen" size="sm" @click="openCreate">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
              New client
            </Button>
          </div>

          <!-- Create / edit form -->
          <div v-if="formOpen" class="rounded-lg border border-border p-4 space-y-3">
            <div class="grid gap-3 md:grid-cols-2">
              <div class="space-y-1.5">
                <Label for="oidc-client-id">Client ID</Label>
                <Input id="oidc-client-id" v-model="form.clientId" :disabled="loading || isEditing" placeholder="e.g. openlist" maxlength="100" />
              </div>
              <div class="space-y-1.5">
                <Label for="oidc-display-name">Display name</Label>
                <Input id="oidc-display-name" v-model="form.displayName" :disabled="loading" placeholder="e.g. OpenList" maxlength="100" />
              </div>
            </div>
            <div class="space-y-1.5">
              <Label for="oidc-type">Type</Label>
              <select
                id="oidc-type"
                v-model="form.type"
                :disabled="loading || isEditing"
                class="h-9 w-full md:w-72 rounded-md border border-input bg-transparent px-3 text-sm text-foreground"
              >
                <option value="confidential">Confidential — server-side app with a secret</option>
                <option value="public">Public — SPA/native, no secret, PKCE required</option>
              </select>
              <p v-if="isEditing" class="text-xs text-muted-foreground">Client ID and type cannot change after creation.</p>
            </div>
            <div class="space-y-1.5">
              <Label for="oidc-redirects">Redirect URIs (one per line — matched exactly, query string included)</Label>
              <textarea
                id="oidc-redirects"
                v-model="form.redirectUrisText"
                :disabled="loading"
                rows="3"
                class="w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm font-mono text-foreground"
                placeholder="https://app.example.com/api/auth/sso_callback"
              ></textarea>
            </div>
            <div class="space-y-1.5">
              <Label>Scopes (besides openid)</Label>
              <div class="flex items-center gap-5">
                <label v-for="scope in ['profile', 'email']" :key="scope" class="flex items-center gap-2 text-sm text-foreground">
                  <input type="checkbox" :value="scope" v-model="form.scopes" :disabled="loading" class="size-4 accent-primary" />
                  {{ scope }}
                </label>
              </div>
            </div>
            <div class="flex items-center gap-2 pt-1">
              <Button size="sm" :disabled="loading || !formValid" @click="submitForm">{{ isEditing ? 'Save' : 'Create' }}</Button>
              <Button variant="ghost" size="sm" :disabled="loading" @click="closeForm">Cancel</Button>
            </div>
          </div>

          <!-- Empty state -->
          <p v-if="!clients.length && !formOpen" class="text-sm text-muted-foreground py-4 text-center">No OIDC clients registered yet</p>

          <!-- Table (md+) -->
          <div v-if="clients.length" class="hidden md:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Client ID</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Redirect URIs</TableHead>
                  <TableHead>Scopes</TableHead>
                  <TableHead class="w-px"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableRow v-for="client in clients" :key="client.clientId">
                  <TableCell>
                    <code class="text-xs text-muted-foreground font-mono bg-secondary px-2 py-0.5 rounded">{{ client.clientId }}</code>
                  </TableCell>
                  <TableCell class="font-medium text-foreground">{{ client.displayName }}</TableCell>
                  <TableCell>
                    <Badge :variant="client.type === 'public' ? 'secondary' : 'default'">{{ client.type }}</Badge>
                  </TableCell>
                  <TableCell class="text-muted-foreground">
                    <div class="max-w-72 space-y-0.5">
                      <div v-for="uri in client.redirectUris" :key="uri" class="truncate text-xs font-mono" :title="uri">{{ uri }}</div>
                    </div>
                  </TableCell>
                  <TableCell class="text-muted-foreground text-xs">{{ client.scopes?.join(', ') || '—' }}</TableCell>
                  <TableCell>
                    <div class="flex items-center justify-end gap-1">
                      <button :class="iconButtonClass" title="Edit" :disabled="loading" @click="openEdit(client)">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/></svg>
                      </button>
                      <button
                        v-if="client.type !== 'public'"
                        :class="iconButtonClass"
                        title="Regenerate secret"
                        :disabled="loading"
                        @click="handleRegenerate(client.clientId!)"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-2.64-6.36"/><polyline points="21 3 21 9 15 9"/></svg>
                      </button>
                      <button
                        :class="cn(iconButtonClass, 'hover:bg-destructive/10 hover:text-destructive')"
                        title="Delete"
                        :disabled="loading"
                        @click="handleDelete(client.clientId!)"
                      >&#x2715;</button>
                    </div>
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>

          <!-- Card stack (mobile) -->
          <div v-if="clients.length" class="md:hidden flex flex-col gap-2">
            <div v-for="client in clients" :key="client.clientId" class="rounded-lg bg-muted p-3.5 space-y-2">
              <div class="flex items-center justify-between gap-2">
                <span class="text-sm font-medium text-foreground">{{ client.displayName }}</span>
                <div class="flex items-center gap-1.5">
                  <Badge :variant="client.type === 'public' ? 'secondary' : 'default'">{{ client.type }}</Badge>
                  <button :class="iconButtonClass" title="Edit" :disabled="loading" @click="openEdit(client)">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/></svg>
                  </button>
                  <button
                    :class="cn(iconButtonClass, 'hover:bg-destructive/10 hover:text-destructive')"
                    title="Delete"
                    :disabled="loading"
                    @click="handleDelete(client.clientId!)"
                  >&#x2715;</button>
                </div>
              </div>
              <code class="block text-xs text-muted-foreground font-mono bg-secondary px-2 py-0.5 rounded w-fit">{{ client.clientId }}</code>
              <div class="text-xs text-muted-foreground font-mono space-y-0.5">
                <div v-for="uri in client.redirectUris" :key="uri" class="truncate">{{ uri }}</div>
              </div>
              <Button
                v-if="client.type !== 'public'"
                variant="outline"
                size="sm"
                :disabled="loading"
                @click="handleRegenerate(client.clientId!)"
              >Regenerate secret</Button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>

    <Toast :message="error" variant="error" />
    <Toast :message="success" variant="success" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { cn } from '@/lib/utils'
import PageHeader from '@/components/PageHeader.vue'
import Toast from '@/components/Toast.vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import * as api from '@/api'
import type { OidcClientSummary } from '@/api'

const iconButtonClass =
  'inline-flex items-center justify-center size-[26px] rounded-full text-muted-foreground hover:bg-accent hover:text-foreground transition-colors disabled:opacity-40'

const clients = ref<OidcClientSummary[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)

const issuedSecret = ref<string | null>(null)
const issuedSecretClientId = ref('')
const copied = ref(false)

const formOpen = ref(false)
const isEditing = ref(false)
const form = ref({
  clientId: '',
  displayName: '',
  type: 'confidential' as 'confidential' | 'public',
  redirectUrisText: '',
  scopes: ['profile', 'email'] as string[],
})

const redirectUris = computed(() =>
  form.value.redirectUrisText.split('\n').map((u) => u.trim()).filter(Boolean))

const formValid = computed(() =>
  form.value.clientId.trim().length > 0
  && form.value.displayName.trim().length > 0
  && redirectUris.value.length > 0)

async function loadClients() {
  try {
    clients.value = await api.listOidcClients()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load OIDC clients'
  }
}

function openCreate() {
  isEditing.value = false
  form.value = { clientId: '', displayName: '', type: 'confidential', redirectUrisText: '', scopes: ['profile', 'email'] }
  formOpen.value = true
}

function openEdit(client: OidcClientSummary) {
  isEditing.value = true
  form.value = {
    clientId: client.clientId!,
    displayName: client.displayName ?? '',
    type: (client.type as 'confidential' | 'public') ?? 'confidential',
    redirectUrisText: (client.redirectUris ?? []).join('\n'),
    scopes: [...(client.scopes ?? [])],
  }
  formOpen.value = true
}

function closeForm() {
  formOpen.value = false
}

async function submitForm() {
  if (!formValid.value) return
  loading.value = true
  error.value = null
  try {
    if (isEditing.value) {
      await api.updateOidcClient(form.value.clientId, {
        displayName: form.value.displayName.trim(),
        redirectUris: redirectUris.value,
        scopes: form.value.scopes,
      })
      success.value = `Client "${form.value.clientId}" updated`
    } else {
      const result = await api.createOidcClient({
        clientId: form.value.clientId.trim(),
        displayName: form.value.displayName.trim(),
        type: form.value.type,
        redirectUris: redirectUris.value,
        scopes: form.value.scopes,
      })
      if (result.clientSecret) {
        issuedSecret.value = result.clientSecret
        issuedSecretClientId.value = form.value.clientId.trim()
        copied.value = false
      }
      success.value = `Client "${form.value.clientId}" created`
    }
    formOpen.value = false
    await loadClients()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Request failed'
  } finally {
    loading.value = false
  }
}

async function handleRegenerate(clientId: string) {
  if (!confirm(`Regenerate the secret for "${clientId}"? The current secret stops working immediately.`)) return
  loading.value = true
  try {
    const result = await api.regenerateOidcClientSecret(clientId)
    issuedSecret.value = result.clientSecret!
    issuedSecretClientId.value = clientId
    copied.value = false
    success.value = `Secret for "${clientId}" regenerated`
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to regenerate secret'
  } finally {
    loading.value = false
  }
}

async function handleDelete(clientId: string) {
  if (!confirm(`Delete OIDC client "${clientId}"? Apps using it can no longer sign in.`)) return
  loading.value = true
  try {
    await api.deleteOidcClient(clientId)
    await loadClients()
    success.value = `Client "${clientId}" deleted`
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to delete client'
  } finally {
    loading.value = false
  }
}

async function copySecret() {
  if (!issuedSecret.value) return
  try {
    await navigator.clipboard.writeText(issuedSecret.value)
    copied.value = true
  } catch {
    // Fallback: user selects manually
  }
}

onMounted(loadClients)
</script>
