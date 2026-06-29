<template>
  <Transition
    enter-active-class="transition duration-200 ease-out"
    enter-from-class="opacity-0 translate-y-2"
    leave-active-class="transition duration-150 ease-in"
    leave-to-class="opacity-0 translate-y-2"
  >
    <div
      v-if="message"
      :class="cn(
        'fixed bottom-6 left-1/2 -translate-x-1/2 z-[1000] px-6 py-3 rounded-lg text-sm font-medium shadow-md border backdrop-blur-glass',
        variant === 'error'
          ? 'bg-destructive/10 text-destructive border-destructive/30'
          : 'bg-primary-soft text-primary-dark border-primary/30',
      )"
      role="status"
    >
      {{ message }}
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { cn } from '@/lib/utils'

defineProps<{
  message: string | null
  variant: 'error' | 'success'
}>()
</script>
