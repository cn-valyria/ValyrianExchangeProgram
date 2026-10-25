<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white">
      Deal Health by Pairing
    </p>
    <p class="text-sm text-gray-500 dark:text-gray-400 mb-3">
      Active deals, by the kind of slots they pair.
    </p>

    <div class="flex flex-wrap gap-x-4 gap-y-1 mb-4">
      <span
        v-for="bucket in DEAL_HEALTH_ORDER"
        :key="bucket"
        class="flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-300"
        :title="DEAL_HEALTH_INFO[bucket].detail"
      >
        <span class="size-2.5 rounded-sm" :class="HEALTH_COLORS[bucket]" />
        {{ DEAL_HEALTH_INFO[bucket].label }}
      </span>
    </div>

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
        <div class="flex h-3 gap-0.5">
          <div
            v-for="bucket in DEAL_HEALTH_ORDER.filter((b) => row.health[b] > 0)"
            :key="bucket"
            class="h-3 first:rounded-l last:rounded-r"
            :class="HEALTH_COLORS[bucket]"
            :style="{ width: `${(row.health[bucket] / row.total) * 100}%` }"
            :title="`${DEAL_HEALTH_INFO[bucket].label}: ${row.health[bucket]}`"
          />
        </div>
        <p class="text-xs text-gray-600 dark:text-gray-300 mt-1">
          {{
            DEAL_HEALTH_ORDER.filter((b) => row.health[b] > 0)
              .map(
                (b) =>
                  `${row.health[b]} ${DEAL_HEALTH_INFO[b].label.toLowerCase()}`,
              )
              .join(' · ')
          }}
        </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { DealHealth } from '~/stores/admin'
import {
  DEAL_HEALTH_INFO,
  DEAL_HEALTH_ORDER,
  type SlotMarket,
} from '~/infrastructure/dashboard'

interface Props {
  markets: SlotMarket[]
  otherDeals: Record<DealHealth, number>
}

const props = defineProps<Props>()

const HEALTH_COLORS: Record<DealHealth, string> = {
  [DealHealth.OnTrack]: 'bg-green-500',
  [DealHealth.Starting]: 'bg-gray-400 dark:bg-gray-500',
  [DealHealth.Stalled]: 'bg-amber-500',
  [DealHealth.AtRisk]: 'bg-red-500',
}

const total = (health: Record<DealHealth, number>): number =>
  DEAL_HEALTH_ORDER.reduce((sum, b) => sum + health[b], 0)

const rows = computed(() => {
  const rows = props.markets
    .filter((m) => m.activeDeals > 0)
    .sort((a, b) => b.activeDeals - a.activeDeals)
    .map((m) => ({
      key: m.key,
      label: m.second ? `${m.first.name} ↔ ${m.second.name}` : m.first.name,
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
