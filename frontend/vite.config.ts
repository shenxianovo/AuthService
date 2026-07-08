import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'
import mockApiPlugin from './mock/plugin'

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const useMock = mode === 'mock'

  return {
    plugins: [
      vue(),
      tailwindcss(),
      useMock && mockApiPlugin(),
    ].filter(Boolean),
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url))
      }
    },
    server: {
      proxy: useMock ? undefined : {
        '/api': {
          target: 'http://localhost:5252',
          changeOrigin: true
        },
        // OIDC provider endpoints (OpenIddict) for local end-to-end testing
        '/connect': {
          target: 'http://localhost:5252',
          changeOrigin: true
        },
        '/.well-known': {
          target: 'http://localhost:5252',
          changeOrigin: true
        }
      }
    }
  }
})
