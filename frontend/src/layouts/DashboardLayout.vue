<template>
  <div class="flex min-h-screen">
    <!-- Sidebar -->
    <aside
      :class="cn(
        'w-60 bg-sidebar border-r border-sidebar-border flex flex-col fixed inset-y-0 left-0 z-[200] transition-transform duration-200',
        sidebarOpen ? 'translate-x-0' : '-translate-x-full md:translate-x-0',
      )"
    >
      <div class="flex items-center gap-2.5 px-5 py-6 text-[17px] font-bold text-sidebar-foreground">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="text-primary"><path d="M12 2a5 5 0 0 1 5 5v3a5 5 0 0 1-10 0V7a5 5 0 0 1 5-5z"/><path d="M3.5 14.5C3.5 9 7 8 12 8s8.5 1 8.5 6.5c0 4-3.5 7.5-8.5 7.5s-8.5-3.5-8.5-7.5z"/><path d="M8 14v.5"/><path d="M16 14v.5"/></svg>
        <span>AuthService</span>
      </div>

      <nav class="flex-1 flex flex-col gap-0.5 px-3">
        <router-link v-for="item in navItems" :key="item.to" :to="item.to" :class="navItemClass" active-class="bg-sidebar-accent text-sidebar-accent-foreground font-semibold" @click="sidebarOpen = false">
          <span v-html="item.icon" class="inline-flex"></span>
          <span>{{ item.label }}</span>
        </router-link>
      </nav>

      <div class="p-3 border-t border-sidebar-border">
        <button :class="cn(navItemClass, 'hover:bg-destructive/10 hover:text-destructive')" @click="handleLogout">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
          <span>Sign out</span>
        </button>
      </div>
    </aside>

    <!-- Main content -->
    <div class="flex-1 md:ml-60 flex flex-col min-h-screen">
      <header class="sticky top-0 z-[100] h-[60px] px-4 md:px-8 flex items-center justify-end bg-glass/80 backdrop-blur-glass border-b border-border">
        <button class="md:hidden mr-auto inline-flex items-center justify-center size-9 rounded-md text-muted-foreground hover:bg-accent" @click="sidebarOpen = !sidebarOpen">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="3" y1="12" x2="21" y2="12"/><line x1="3" y1="6" x2="21" y2="6"/><line x1="3" y1="18" x2="21" y2="18"/></svg>
        </button>
        <div class="flex items-center gap-2.5">
          <div class="size-9 rounded-full bg-primary text-primary-foreground flex items-center justify-center text-sm font-bold">{{ userInitial }}</div>
          <span class="text-sm font-medium text-foreground">{{ userStore.userInfo.value?.displayName ?? 'User' }}</span>
        </div>
      </header>

      <main class="flex-1 p-5 md:p-8 w-full max-w-[1100px]">
        <router-view />
      </main>
    </div>

    <!-- Mobile sidebar overlay -->
    <div v-if="sidebarOpen" class="md:hidden fixed inset-0 z-[150] bg-black/30" @click="sidebarOpen = false"></div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { cn } from '@/lib/utils'
import { authStore } from '@/stores/auth'
import { userStore } from '@/stores/user'
import * as api from '@/api'

const router = useRouter()
const sidebarOpen = ref(false)

const navItemClass =
  'flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground transition-colors w-full text-left'

const navItems = [
  { to: '/dashboard/profile', label: 'Profile', icon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>' },
  { to: '/dashboard/emails', label: 'Email', icon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="4" width="20" height="16" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg>' },
  { to: '/dashboard/providers', label: 'Linked Accounts', icon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>' },
  { to: '/dashboard/api-keys', label: 'API Keys', icon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4"/></svg>' },
  { to: '/dashboard/security', label: 'Security', icon: '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>' },
]

const userInitial = computed(() => {
  const n = userStore.userInfo.value?.displayName
  return n ? n.charAt(0).toUpperCase() : '?'
})

async function handleLogout() {
  await api.logout()
  authStore.clearTokens()
  userStore.clear()
  router.push('/login')
}
</script>
