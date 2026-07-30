<template>
  <div class="min-h-screen flex items-center justify-center p-5">
    <div
      class="w-full max-w-[420px] rounded-xl border border-glass-border bg-glass shadow-md backdrop-blur-glass overflow-hidden"
    >
      <div class="flex justify-end px-3 pt-3">
        <LanguageSwitcher />
      </div>
      <div class="text-center px-8 pt-2">
        <h1 class="text-2xl font-bold text-foreground">AuthService</h1>
        <p v-if="clientId" class="mt-2 text-sm text-muted-foreground">
          <i18n-t keypath="authLayout.signInToContinue" tag="span">
            <template #client><strong class="text-foreground">{{ clientId }}</strong></template>
          </i18n-t>
        </p>
        <p v-else class="mt-2 text-sm text-muted-foreground">{{ t('authLayout.signInToAccount') }}</p>
      </div>
      <div class="px-8 pb-8 pt-6">
        <router-view />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import LanguageSwitcher from '@/components/LanguageSwitcher.vue'
import { useOidcReturnUrl } from '@/stores/oidcReturnUrl'

const { t } = useI18n()
const { clientId } = useOidcReturnUrl()
</script>
