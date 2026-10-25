<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white">
      Deal Health by Pairing
    </p>
    <p class="text-sm text-gray-500 dark:text-gray-400 mb-3">
      Active deals, by the kind of slots they pair.
    </p>

    <AdminDashboardHealthLegend class="mb-4" />

    <p
      v-if="rows.length === 0"
      class="text-sm text-gray-500 dark:text-gray-400"
    >
      No active deals.
    </p>

    <div class="space-y-4">
      <div v-for="row in rows" :key="row.key" data-pairing>
        <div class="flex justify-between gap-2 mb-1">
          <span class="text-sm font-medium text-gray-900 dark:text-white">
            {{ row.label }}
          </span>
          <span class="text-sm text-gray-500 dark:text-gray-400">
            {{ row.total }} deals
          </span>
        </div>
        <p class="text-xs text-gray-500 dark:text-gray-400 mb-1.5">
          {{ row.terms }}
        </p>
        <AdminDashboardHealthBar :health="row.health" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { DealHealth } from '~/stores/admin'
import {
  DEAL_HEALTH_ORDER,
  marketLabel,
  type SlotMarket,
} from '~/infrastructure/dashboard'

interface Props {
  markets: SlotMarket[]
  otherDeals: Record<DealHealth, number>
}

const props = defineProps<Props>()

const total = (health: Record<DealHealth, number>): number =>
  DEAL_HEALTH_ORDER.reduce((sum, b) => sum + health[b], 0)

const rows = computed(() => {
  const rows = props.markets
    .filter((m) => m.activeDeals > 0)
    .sort((a, b) => b.activeDeals - a.activeDeals)
    .map((m) => ({
      key: m.key,
      label: marketLabel(m),
      terms: m.first.terms,
      health: m.health,
      total: m.activeDeals,
    }))
  const otherTotal = total(props.otherDeals)
  if (otherTotal > 0) {
    rows.push({
      key: 'other',
      label: 'Other pairings',
      terms: "Manual deals between slots that aren't exact opposites",
      health: props.otherDeals,
      total: otherTotal,
    })
  }
  return rows
})
</script>
