<template>
  <EmailVerificationView
    :email="email"
    :loading="loading"
    :error="error"
    @verify="handleVerify"
    @resend="handleResend"
  />
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import EmailVerificationView from '@/components/EmailVerificationView.vue'
import { userStore } from '@/stores/user'
import * as api from '@/api'

const route = useRoute()
const router = useRouter()

const loading = ref(false)
const error = ref('')

const email = computed(() => (route.query.email as string) ?? '')
const emailId = computed(() => (route.query.emailId as string) ?? null)

async function handleVerify(code: string) {
  error.value = ''
  loading.value = true
  try {
    await api.verifyEmail(code, emailId.value ?? undefined)
    await userStore.fetch()
    router.push('/dashboard/profile')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Verification failed'
  } finally {
    loading.value = false
  }
}

async function handleResend() {
  error.value = ''
  try {
    await api.sendVerificationCode(emailId.value ?? undefined)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to resend code'
  }
}
</script>
