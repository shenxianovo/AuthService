<template>
  <div>
    <PageHeader title="Profile" />

    <Card v-if="userStore.userInfo.value">
      <CardContent class="flex items-center gap-5">
        <div class="size-16 rounded-full bg-primary text-primary-foreground flex items-center justify-center text-3xl font-bold shrink-0">
          {{ userInitial }}
        </div>
        <div class="min-w-0">
          <h2 class="text-2xl font-bold text-foreground mb-1">{{ userStore.userInfo.value?.displayName ?? 'User' }}</h2>
          <p class="text-xs text-muted-foreground font-mono break-all">{{ authStore.state.tokens?.userId }}</p>
        </div>
      </CardContent>
    </Card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { Card, CardContent } from '@/components/ui/card'
import PageHeader from '@/components/PageHeader.vue'
import { authStore } from '@/stores/auth'
import { userStore } from '@/stores/user'

const userInitial = computed(() => {
  const n = userStore.userInfo.value?.displayName
  return n ? n.charAt(0).toUpperCase() : '?'
})

onMounted(() => {
  if (!userStore.userInfo.value) {
    userStore.fetch()
  }
})
</script>
