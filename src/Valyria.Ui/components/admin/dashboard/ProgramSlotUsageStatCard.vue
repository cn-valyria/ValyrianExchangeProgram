<template>
  <div class="text-center space-y-2">
    <p class="text-sm text-gray-500 dark:text-gray-400">Slots in Deals</p>
    <p class="text-3xl font-bold text-gray-900 dark:text-white">
      {{ usagePercentage }}%
    </p>
    <p class="text-xs text-gray-500 dark:text-gray-400">
      ({{ data?.currentSlotsUsed ?? 0 }} / {{ data?.maxCapacity ?? 0 }})
    </p>
  </div>
</template>

<script setup lang="ts">
interface Props {
  data: SlotUsageSummary | undefined
}

const props = defineProps<Props>()

const usagePercentage = computed<string>(() => {
  if (!props.data?.currentSlotsUsed || !props.data?.maxCapacity) {
    return '0.00'
  }
  return ((props.data.currentSlotsUsed / props.data.maxCapacity) * 100).toFixed(
    2,
  )
})
</script>
