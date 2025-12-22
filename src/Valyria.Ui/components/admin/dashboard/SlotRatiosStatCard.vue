<template>
  <div>
    <p class="text-sm text-gray-500 dark:text-gray-400 mb-4 text-center">Incoming/Outgoing Slot Ratios</p>
    <div class="flex items-center justify-center gap-6">
      <div class="text-center space-y-1">
        <p class="text-xs text-gray-500 dark:text-gray-400">Cash Ratio</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ cashRatio }}</p>
      </div>
      <div class="text-4xl font-light text-gray-400 dark:text-gray-600">/</div>
      <div class="text-center space-y-1">
        <p class="text-xs text-gray-500 dark:text-gray-400">Tech Ratio</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ techRatio }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  data: AidRatioSummary | undefined
}

const props = defineProps<Props>()

const cashRatio = computed<string>(() => {
  if (!props.data?.totalIncomingCashSlots || !props.data?.totalOutgoingCashSlots) {
    return '0.00'
  }
  return (props.data.totalIncomingCashSlots / props.data.totalOutgoingCashSlots).toFixed(2)
})

const techRatio = computed<string>(() => {
  if (!props.data?.totalIncomingTechSlots || !props.data?.totalOutgoingTechSlots) {
    return '0.00'
  }
  return (props.data.totalIncomingTechSlots / props.data.totalOutgoingTechSlots).toFixed(2)
})
</script>
