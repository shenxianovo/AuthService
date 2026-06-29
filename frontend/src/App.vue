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
