import { ref } from 'vue'
import type { UserInfoResponse } from '@/api'
import * as api from '@/api'

const userInfo = ref<UserInfoResponse | null>(null)
const loading = ref(false)

async function fetch() {
  loading.value = true
  try {
    userInfo.value = await api.fetchMe()
  } catch {
    userInfo.value = null
  } finally {
    loading.value = false
  }
}

function clear() {
  userInfo.value = null
}

export const userStore = {
  userInfo,
  loading,
  fetch,
  clear,
}
