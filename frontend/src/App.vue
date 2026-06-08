<template>
  <router-view />
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useExternalRedirect } from '@/stores/externalRedirect'

const { init } = useExternalRedirect()

onMounted(() => {
  init()
})
</script>

<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
</style>
