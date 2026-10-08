<template>
  <div class="flex flex-col items-center gap-2 min-w-fit">
    <!-- Count & Badge Row -->
    <div class="flex items-center gap-3">
      <div class="text-center">
        <div class="text-3xl font-bold" :class="statusClass">
          {{ hasDeals ? attentionCount : '--' }}
        </div>
        <div class="text-sm font-medium text-gray-600 dark:text-gray-400">
          Deals Needing Action
        </div>
      </div>
      <UBadge :color="badgeColor" size="md" variant="subtle" class="shrink-0">
        {{ statusLabel }}
      </UBadge>
    </div>

    <!-- Description Row -->
    <div class="text-xs text-gray-600 dark:text-gray-400 text-center max-w-xs">
      {{ description }}
    </div>
  </div>
</template>

<script setup lang="ts">
import type { DealDetail, DealNotification } from '~/stores/account'

interface Props {
  deals: DealDetail[]
  notifications: DealNotification[]
}

const props = withDefaults(defineProps<Props>(), {
  deals: () => [],
  notifications: () => []
})

const hasDeals = computed(() => props.deals.length > 0)

const flaggedDealIds = computed(() => new Set(props.notifications.map((n) => n.dealId)))

// Deals that are flagged or past the inactivity threshold
const attentionCount = computed(() =>
  props.deals.filter((deal) => deal.needsAction || flaggedDealIds.value.has(deal.id)).length
)

// Deals that aren't urgent yet, but are heading that way
const atRiskCount = computed(() =>
  props.deals.filter((deal) => !deal.needsAction && !flaggedDealIds.value.has(deal.id) && deal.actionNeeded).length
)

const status = computed(() => {
  if (!hasDeals.value) return 'none'
  if (attentionCount.value > 0) return 'attention'
  if (atRiskCount.value > 0) return 'atRisk'
  return 'good'
})

const statusLabel = computed(() => {
  switch (status.value) {
    case 'attention': return 'Attention'
    case 'atRisk': return 'At Risk'
    case 'good': return 'All Good'
    default: return 'No Deals'
  }
})

const statusClass = computed(() => {
  switch (status.value) {
    case 'attention': return 'text-red-600 dark:text-red-400'
    case 'atRisk': return 'text-orange-600 dark:text-orange-400'
    case 'good': return 'text-green-600 dark:text-green-400'
    default: return 'text-gray-400 dark:text-gray-500'
  }
})

const badgeColor = computed(() => {
  switch (status.value) {
    case 'attention': return 'error'
    case 'atRisk': return 'warning'
    case 'good': return 'success'
    default: return 'neutral'
  }
})

const description = computed(() => {
  switch (status.value) {
    case 'attention':
      return `${attentionCount.value} of your deals ${attentionCount.value === 1 ? 'has' : 'have'} gone too long without aid. Exchange aid with ${attentionCount.value === 1 ? 'that partner' : 'those partners'} soon, or the deal${attentionCount.value === 1 ? '' : 's'} may be cancelled.`
    case 'atRisk':
      return `${atRiskCount.value} of your deals ${atRiskCount.value === 1 ? 'has' : 'have'} had no recent aid. Check the deals below to see what's needed.`
    case 'good':
      return 'All of your deals are active. Nice work!'
    default:
      return "You don't have any active deals yet. Your deal health will show here once you're matched with a partner."
  }
})
</script>
