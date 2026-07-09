<template>
  <router-view />
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useOidcReturnUrl } from '@/stores/oidcReturnUrl'

const { init: initOidcReturnUrl } = useOidcReturnUrl()

onMounted(() => {
  initOidcReturnUrl()
})
</script>
