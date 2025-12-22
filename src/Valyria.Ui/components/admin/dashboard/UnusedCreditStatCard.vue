<template>
  <div class="text-center space-y-2">
    <p class="text-sm text-gray-500 dark:text-gray-400">Unused Credit</p>
    <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ data?.unusedCredit ?? 0 }}</p>
    <p class="text-xs text-gray-500 dark:text-gray-400">"Rate" Balance Currently Available</p>
  </div>
</template>

<script setup lang="ts">
interface Props {
  data: UnusedCreditSummary | undefined
}

const props = defineProps<Props>()
</script>
