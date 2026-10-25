<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white">
      Where Open Slots Are
    </p>
    <p class="text-sm text-gray-500 dark:text-gray-400 mb-4">
      {{ breakdown.total }} open slots. The wait queue counts
      {{ breakdown.waitQueue }} of them: everything except slots on flagged
      accounts.
    </p>

    <div
      v-if="breakdown.total > 0"
      class="flex h-3 gap-0.5 mb-4"
      role="img"
      :aria-label="rows.map((r) => `${r.label}: ${r.value}`).join(', ')"
    >
      <div
        v-for="row in rows.filter((r) => r.value > 0)"
        :key="row.label"
        class="h-3 first:rounded-l last:rounded-r"
        :class="row.color"
        :style="{ width: `${(row.value / breakdown.total) * 100}%` }"
        :title="`${row.label}: ${row.value}`"
      />
    </div>

    <div class="space-y-3">
      <div v-for="row in rows" :key="row.label" class="flex gap-3">
        <span class="mt-1.5 size-2.5 rounded-sm shrink-0" :class="row.color" />
        <div class="flex-1">
          <div class="flex justify-between gap-2">
            <span class="text-sm font-medium text-gray-900 dark:text-white">
              {{ row.label }}
            </span>
            <span class="text-sm font-semibold text-gray-900 dark:text-white">
              {{ row.value }}
            </span>
          </div>
          <p class="text-xs text-gray-500 dark:text-gray-400">
            {{ row.detail }}
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { OpenSlotBreakdown } from '~/infrastructure/dashboard'

interface Props {
  breakdown: OpenSlotBreakdown
}

const props = defineProps<Props>()

const rows = computed(() => [
  {
    label: 'Waiting for partners',
    value: props.breakdown.waiting,
    detail:
      'More open slots than the opposite side has. New accounts on the opposite side would fill these.',
    color: 'bg-primary-500',
  },
  {
    label: 'Unpaired',
    value: props.breakdown.unpaired,
    detail:
      'Both sides have open slots, but the accounts already share a deal or were paired before. More accounts on either side would free these up.',
    color: 'bg-primary-300 dark:bg-primary-700',
  },
  {
    label: 'Never matched',
    value: props.breakdown.unmatchable,
    detail:
      'Slots set up so that nothing pairs with them, such as slots with no cycles. Fix them in the account’s slots.',
    color: 'bg-gray-400 dark:bg-gray-500',
  },
  {
    label: 'On flagged accounts',
    value: props.breakdown.flagged,
    detail:
      'Left out of the wait queue until their flags are resolved on the Deals page.',
    color: 'bg-red-500',
  },
])
</script>
