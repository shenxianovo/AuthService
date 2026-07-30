<template>
  <div>
    <PageHeader :title="t('profile.title')" :description="greeting" />

    <div v-if="userStore.userInfo.value" class="space-y-6">
      <!-- Profile hero -->
      <Card>
        <CardContent class="flex items-center gap-5">
          <div class="size-16 rounded-full bg-primary text-primary-foreground flex items-center justify-center text-3xl font-bold shrink-0">
            {{ userInitial }}
          </div>
          <div class="min-w-0">
            <h2 class="text-2xl font-bold text-foreground mb-0.5">{{ userStore.userInfo.value.displayName ?? t('nav.user') }}</h2>
            <p class="text-sm text-muted-foreground">{{ primaryEmail ?? t('profile.noPrimaryEmail') }}</p>
            <p v-if="memberSince" class="text-xs text-muted-foreground mt-1">{{ t('profile.memberSince', { date: memberSince }) }}</p>
          </div>
        </CardContent>
      </Card>

      <!-- Stat cards -->
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <RouterLink to="/dashboard/emails" class="group">
          <Card class="transition-shadow group-hover:shadow-md">
            <CardContent class="space-y-1">
              <p class="text-sm text-muted-foreground">{{ t('profile.verifiedEmails') }}</p>
              <p class="text-3xl font-bold text-foreground">{{ verifiedEmailCount }}<span class="text-base font-normal text-muted-foreground"> / {{ emailCount }}</span></p>
            </CardContent>
          </Card>
        </RouterLink>

        <RouterLink to="/dashboard/providers" class="group">
          <Card class="transition-shadow group-hover:shadow-md">
            <CardContent class="space-y-1">
              <p class="text-sm text-muted-foreground">{{ t('profile.linkedAccounts') }}</p>
              <p class="text-3xl font-bold text-foreground">{{ providerCount }}</p>
            </CardContent>
          </Card>
        </RouterLink>

        <RouterLink to="/dashboard/api-keys" class="group">
          <Card class="transition-shadow group-hover:shadow-md">
            <CardContent class="space-y-1">
              <p class="text-sm text-muted-foreground">{{ t('profile.activeApiKeys') }}</p>
              <p class="text-3xl font-bold text-foreground">
                <span v-if="apiKeyCount === null" class="text-muted-foreground">—</span>
                <span v-else>{{ apiKeyCount }}</span>
              </p>
            </CardContent>
          </Card>
        </RouterLink>
      </div>

      <!-- Account status -->
      <Card>
        <CardHeader>
          <CardTitle>{{ t('profile.accountStatus') }}</CardTitle>
        </CardHeader>
        <CardContent class="space-y-3">
          <div class="flex items-center justify-between gap-3 py-2 border-b border-border last:border-0">
            <div class="flex items-center gap-2.5">
              <span :class="statusDotClass(hasPassword)"></span>
              <span class="text-sm text-foreground">{{ t('profile.password') }}</span>
            </div>
            <RouterLink to="/dashboard/security" class="text-sm text-muted-foreground hover:text-foreground hover:underline">
              {{ hasPassword ? t('profile.passwordSet') : t('profile.addPassword') }}
            </RouterLink>
          </div>

          <div class="flex items-center justify-between gap-3 py-2 border-b border-border last:border-0">
            <div class="flex items-center gap-2.5">
              <span :class="statusDotClass(allEmailsVerified)"></span>
              <span class="text-sm text-foreground">{{ t('profile.emailVerification') }}</span>
            </div>
            <RouterLink to="/dashboard/emails" class="text-sm text-muted-foreground hover:text-foreground hover:underline">
              {{ allEmailsVerified ? t('profile.allVerified') : t('profile.unverified', { n: unverifiedCount }) }}
            </RouterLink>
          </div>

          <div class="flex items-center justify-between gap-3 py-2 border-b border-border last:border-0">
            <div class="flex items-center gap-2.5">
              <span :class="statusDotClass(providerCount > 0)"></span>
              <span class="text-sm text-foreground">{{ t('profile.linkedAccounts') }}</span>
            </div>
            <RouterLink to="/dashboard/providers" class="text-sm text-muted-foreground hover:text-foreground hover:underline">
              {{ providerCount > 0 ? t('profile.connected', { n: providerCount }) : t('profile.connectOne') }}
            </RouterLink>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import PageHeader from '@/components/PageHeader.vue'
import { userStore } from '@/stores/user'
import * as api from '@/api'

const { t, locale } = useI18n()

const apiKeyCount = ref<number | null>(null)

const userInitial = computed(() => {
  const n = userStore.userInfo.value?.displayName
  return n ? n.charAt(0).toUpperCase() : '?'
})

const greeting = computed(() => {
  const n = userStore.userInfo.value?.displayName
  return n ? t('profile.welcome', { name: n }) : t('profile.welcomeAnon')
})

const emails = computed(() => userStore.userInfo.value?.emails ?? [])
const emailCount = computed(() => emails.value.length)
const verifiedEmailCount = computed(() => emails.value.filter(e => e.isVerified).length)
const unverifiedCount = computed(() => emailCount.value - verifiedEmailCount.value)
const allEmailsVerified = computed(() => emailCount.value > 0 && unverifiedCount.value === 0)
const primaryEmail = computed(() => emails.value.find(e => e.isPrimary)?.email ?? emails.value[0]?.email ?? null)

const providerCount = computed(() => userStore.userInfo.value?.providers?.length ?? 0)
const hasPassword = computed(() => userStore.userInfo.value?.hasPassword ?? false)

const memberSince = computed(() => {
  const d = userStore.userInfo.value?.createdAt
  if (!d) return null
  const date = d instanceof Date ? d : new Date(d as unknown as string)
  if (Number.isNaN(date.getTime())) return null
  return date.toLocaleDateString(locale.value, { month: 'long', year: 'numeric' })
})

function statusDotClass(ok: boolean): string {
  return `size-2 rounded-full shrink-0 ${ok ? 'bg-primary' : 'bg-amber-400'}`
}

onMounted(async () => {
  if (!userStore.userInfo.value) {
    await userStore.fetch()
  }
  try {
    const keys = await api.listApiKeys()
    apiKeyCount.value = keys.filter(k => !k.isRevoked).length
  } catch {
    apiKeyCount.value = null
  }
})
</script>
