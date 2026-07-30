<template>
  <button
    class="inline-flex items-center gap-1.5 h-9 px-2.5 rounded-md text-sm text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
    :title="nextLabel"
    @click="toggle"
  >
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M2 12h20"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/></svg>
    <span>{{ nextLabel }}</span>
  </button>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { setLocale } from '@/i18n'
import type { Locale } from '@/i18n'

const { locale } = useI18n()

// The button shows the language you'd switch TO, in that language.
const nextLocale = computed<Locale>(() => (locale.value === 'zh-CN' ? 'en' : 'zh-CN'))
const nextLabel = computed(() => (nextLocale.value === 'zh-CN' ? '中文' : 'English'))

function toggle() {
  setLocale(nextLocale.value)
}
</script>
