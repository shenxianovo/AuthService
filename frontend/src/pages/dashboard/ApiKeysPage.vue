<template>
  <div>
    <PageHeader :title="t('apiKeys.title')" :description="t('apiKeys.description')" />

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
import { useI18n } from 'vue-i18n'
import { Card, CardContent } from '@/components/ui/card'
import PageHeader from '@/components/PageHeader.vue'
import Toast from '@/components/Toast.vue'
import ApiKeysView from '@/components/ApiKeysView.vue'

const { t } = useI18n()

const loading = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)
</script>
