import { createApp } from 'vue'
import App from './App.vue'
import { configureAuthClient } from '@/api/AuthClientBase'
import { authStore } from '@/stores/auth'

// Wire up the global auth client with token store + logout callback
configureAuthClient(
  authStore.asTokenStore(),
  () => {
    // Called when refresh fails — clear state and let App.vue react
    authStore.clearTokens()
  }
)

createApp(App).mount('#app')
