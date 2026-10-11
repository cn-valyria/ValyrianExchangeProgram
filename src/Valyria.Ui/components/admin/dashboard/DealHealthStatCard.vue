<template>
  <div>
    <p class="text-sm text-gray-500 dark:text-gray-400 mb-4 text-center">Deal Health</p>
    <div class="flex items-center justify-center gap-6">
      <div class="text-center space-y-1">
        <p class="text-xs text-gray-500 dark:text-gray-400">Waiting for Deals</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ data?.accountsWaiting ?? 0 }}</p>
        <p class="text-xs text-gray-500 dark:text-gray-400">({{ data?.slotsWaiting ?? 0 }} slots)</p>
      </div>
      <div class="text-4xl font-light text-gray-400 dark:text-gray-600">/</div>
      <div class="text-center space-y-1">
        <p class="text-xs text-gray-500 dark:text-gray-400">Open Flags</p>
        <p class="text-3xl font-bold" :class="(data?.openFlags ?? 0) > 0 ? 'text-red-600 dark:text-red-400' : 'text-gray-900 dark:text-white'">
          {{ data?.openFlags ?? 0 }}
        </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { DealHealthSummary } from '~/stores/admin'

interface Props {
  data: DealHealthSummary | undefined
}

defineProps<Props>()
</script>
