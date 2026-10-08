<template>
  <div>
    <p class="text-sm text-gray-500 dark:text-gray-400 mb-4 text-center">General Program Statistics</p>
    <div class="flex items-center justify-center gap-6">
      <div class="text-center space-y-1">
        <p class="text-xs text-gray-500 dark:text-gray-400">Total Accounts</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ data?.totalAccounts ?? 0 }}</p>
      </div>
      <div class="text-4xl font-light text-gray-400 dark:text-gray-600">/</div>
      <div class="text-center space-y-1">
        <p class="text-xs text-gray-500 dark:text-gray-400">Active Deals</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ data?.activeDeals ?? 0 }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  data: GeneralProgramSummary | undefined
}

const props = defineProps<Props>()
</script>
