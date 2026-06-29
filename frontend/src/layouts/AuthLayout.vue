<template>
  <div class="min-h-screen flex items-center justify-center p-5">
    <div
      class="w-full max-w-[420px] rounded-xl border border-glass-border bg-glass shadow-md backdrop-blur-glass overflow-hidden"
    >
      <div class="text-center px-8 pt-8">
        <h1 class="text-2xl font-bold text-foreground">AuthService</h1>
        <p v-if="externalRedirect" class="mt-2 text-sm text-muted-foreground">
          Sign in to continue to <strong class="text-foreground">{{ externalRedirectHost }}</strong>
        </p>
        <p v-else class="mt-2 text-sm text-muted-foreground">Sign in to your account</p>
      </div>
      <div class="px-8 pb-8 pt-6">
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
