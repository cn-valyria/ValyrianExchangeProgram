<template>
  <div class="flex flex-col items-center gap-2 min-w-fit">
    <!-- Count & Badge Row -->
    <div class="flex items-center gap-3">
      <div class="text-center">
        <div class="text-3xl font-bold" :class="statusClass">
          {{ hasDeals ? actions.length : '--' }}
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
import { getDealActions } from '~/infrastructure/deals'
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

const actions = computed(() => getDealActions(props.deals))

const flaggedCount = computed(() => actions.value.filter((action) => action.flagged).length)

// Deals cancelled over a flag against this account, which pause matching until reviewed
const cancelledFlagCount = computed(() => props.notifications.filter((n) => n.type === 'error').length)

const status = computed(() => {
  if (flaggedCount.value > 0 || cancelledFlagCount.value > 0) return 'flagged'
  if (!hasDeals.value) return 'none'
  if (actions.value.length > 0) return 'action'
  return 'good'
})

const statusLabel = computed(() => {
  switch (status.value) {
    case 'flagged': return 'Flagged'
    case 'action': return 'Action Today'
    case 'good': return 'All Good'
    default: return 'No Deals'
  }
})

const statusClass = computed(() => {
  switch (status.value) {
    case 'flagged': return 'text-red-600 dark:text-red-400'
    case 'action': return 'text-orange-600 dark:text-orange-400'
    case 'good': return 'text-green-600 dark:text-green-400'
    default: return 'text-gray-400 dark:text-gray-500'
  }
})

const badgeColor = computed(() => {
  switch (status.value) {
    case 'flagged': return 'error'
    case 'action': return 'warning'
    case 'good': return 'success'
    default: return 'neutral'
  }
})

const description = computed(() => {
  switch (status.value) {
    case 'flagged':
      return flaggedCount.value > 0
        ? `${flaggedCount.value} of your deals ${flaggedCount.value === 1 ? 'is' : 'are'} flagged. Fix ${flaggedCount.value === 1 ? 'it' : 'them'} below to avoid cancellation. You won't be matched with new partners until then.`
        : "A deal was cancelled over a flag on your account. You won't be matched with new partners until VEP management reviews it."
    case 'action':
      return `${actions.value.length} of your deals ${actions.value.length === 1 ? 'needs' : 'need'} something from you today. See the list below.`
    case 'good':
      return 'Nothing to do today. Your deals are waiting on aid already in progress or on an aid slot opening up.'
    default:
      return "You don't have any active deals yet. Your deal health will show here once you're matched with a partner."
  }
})
</script>
