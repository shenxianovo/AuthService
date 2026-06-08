<template>
  <div class="page">
    <h1 class="page-title">Profile</h1>

    <div class="card">
      <div class="profile-hero">
        <div class="avatar-large">{{ userInitial }}</div>
        <div class="profile-meta">
          <h2 class="profile-name">{{ userStore.userInfo.value?.displayName ?? 'User' }}</h2>
          <p class="profile-id">{{ authStore.state.tokens?.userId }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
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

<style scoped>
.page-title {
  font-size: 22px;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 24px;
}

.card {
  background: #fff;
  border-radius: 14px;
  padding: 24px;
  box-shadow: 0 2px 12px rgba(0,0,0,.05);
}

.profile-hero {
  display: flex;
  align-items: center;
  gap: 20px;
}

.avatar-large {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: #1a1a2e;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  font-weight: 700;
  flex-shrink: 0;
}

.profile-name {
  font-size: 22px;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 4px;
}

.profile-id {
  font-size: 12px;
  color: #999;
  font-family: 'SF Mono', Monaco, 'Cascadia Code', monospace;
  margin: 0;
  word-break: break-all;
}
</style>
