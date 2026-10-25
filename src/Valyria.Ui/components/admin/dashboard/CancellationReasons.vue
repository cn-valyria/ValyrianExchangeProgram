<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white">
      Why Deals Ended, Last {{ days }} Days
    </p>
    <p class="text-sm text-gray-500 dark:text-gray-400 mb-4">
      {{ total }} deals cancelled.
    </p>

    <p v-if="total === 0" class="text-sm text-gray-500 dark:text-gray-400">
      No deals were cancelled.
    </p>

    <div class="space-y-3">
      <div v-for="row in data ?? []" :key="row.reason" data-reason>
        <div class="flex justify-between gap-2 mb-1">
          <span class="text-sm text-gray-700 dark:text-gray-300">
            {{ row.reason }}
          </span>
          <span class="text-sm font-semibold text-gray-900 dark:text-white">
            {{ row.deals }}
          </span>
        </div>
        <div class="h-2 rounded bg-gray-100 dark:bg-gray-800">
          <div
            class="h-2 rounded bg-primary-500"
            :style="{ width: `${(row.deals / max) * 100}%` }"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { CancellationReasonCount } from '~/stores/admin'

interface Props {
  data: CancellationReasonCount[] | undefined
  days: number
}

const props = defineProps<Props>()

const total = computed(() =>
  (props.data ?? []).reduce((sum, row) => sum + row.deals, 0),
)

const max = computed(() =>
  Math.max(1, ...(props.data ?? []).map((row) => row.deals)),
)
</script>
