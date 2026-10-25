<template>
  <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
    <UCard v-for="card in cards" :key="card.title">
      <p class="text-sm text-gray-500 dark:text-gray-400">{{ card.title }}</p>
      <p
        class="text-3xl font-bold mt-1"
        :class="
          card.alert
            ? 'text-red-600 dark:text-red-400'
            : 'text-gray-900 dark:text-white'
        "
      >
        {{ card.value }}
      </p>
      <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">
        {{ card.detail }}
      </p>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import { DealHealth, type ProgramSummary } from '~/stores/admin'
import {
  sumHealth,
  type OpenSlotBreakdown,
  type SlotMarket,
} from '~/infrastructure/dashboard'

interface Props {
  summary: ProgramSummary | undefined
  markets: SlotMarket[]
  otherDeals: Record<DealHealth, number>
  breakdown: OpenSlotBreakdown
}

const props = defineProps<Props>()

const percent = (part: number, whole: number): string =>
  whole > 0 ? `${Math.round((part / whole) * 100)}%` : '0%'

const cards = computed(() => {
  const sides = props.markets.flatMap((m) =>
    m.second ? [m.first, m.second] : [m.first],
  )
  const totalSlots = sides.reduce((sum, s) => sum + s.summary.totalSlots, 0)
  const slotsInDeals = sides.reduce((sum, s) => sum + s.summary.slotsInDeals, 0)
  const health = sumHealth([
    ...props.markets.map((m) => m.health),
    props.otherDeals,
  ])
  const activeDeals = props.summary?.activeDeals ?? 0
  const flaggedAccounts = props.summary?.flaggedAccounts ?? 0

  return [
    {
      title: 'Slots in Deals',
      value: percent(slotsInDeals, totalSlots),
      detail: `${slotsInDeals} of ${totalSlots} slots, across ${props.summary?.totalAccounts ?? 0} accounts`,
      alert: false,
    },
    {
      title: 'Deals On Track',
      value: percent(health[DealHealth.OnTrack], activeDeals),
      detail: `${activeDeals} active · ${health[DealHealth.AtRisk]} at risk · ${health[DealHealth.Stalled]} stalled`,
      alert: false,
    },
    {
      title: 'Slots Waiting for a Match',
      value: String(props.breakdown.waitQueue),
      detail: `${props.breakdown.waiting} need a new partner · plus ${props.breakdown.flagged} on flagged accounts`,
      alert: false,
    },
    {
      title: 'Flagged Accounts',
      value: String(flaggedAccounts),
      detail: `${props.summary?.openFlags ?? 0} open flags · left out of matching until resolved`,
      alert: flaggedAccounts > 0,
    },
  ]
})
</script>
