<template>
  <router-view />
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useExternalRedirect } from '@/stores/externalRedirect'
import { useOidcReturnUrl } from '@/stores/oidcReturnUrl'

const { init } = useExternalRedirect()
const { init: initOidcReturnUrl } = useOidcReturnUrl()

onMounted(() => {
  init()
  initOidcReturnUrl()
})
</script>
