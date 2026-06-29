<template>
  <div>
    <h1 class="text-2xl font-bold text-foreground mb-6">API Keys</h1>

    <Card>
      <CardContent>
        <ApiKeysView
          :loading="loading"
          @error="(msg) => { error = msg }"
          @success="(msg) => { success = msg }"
          @update:loading="(val) => { loading = val }"
        />
      </CardContent>
    </Card>

    <Toast :message="error" variant="error" />
    <Toast :message="success" variant="success" />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { Card, CardContent } from '@/components/ui/card'
import Toast from '@/components/Toast.vue'
import ApiKeysView from '@/components/ApiKeysView.vue'

const loading = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)
</script>
