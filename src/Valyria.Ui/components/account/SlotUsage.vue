<template>
  <div class="flex flex-col items-center gap-2 min-w-fit">
    <!-- Slot Count & Badge Row -->
    <div class="flex items-center gap-3">
      <div class="text-center">
        <div class="text-3xl font-bold" :class="statusClass">
          {{ isOnHold ? '--' : `${slotsUsed} / ${slotsTotal}` }}
        </div>
        <div class="text-sm font-medium text-gray-600 dark:text-gray-400">
          Slots in Deals
        </div>
      </div>
      <UBadge :color="badgeColor" size="md" variant="subtle" class="shrink-0">
        {{ statusLabel }}
      </UBadge>
    </div>

    <UProgress
      v-if="!isOnHold"
      :model-value="slotsUsed"
      :max="Math.max(slotsTotal, 1)"
      :color="badgeColor"
      size="sm"
      class="w-40"
    />

    <!-- Description Row -->
    <div class="text-xs text-gray-600 dark:text-gray-400 text-center max-w-xs">
      {{ description }}
    </div>

    <!-- Team event bonus slot -->
    <UBadge
      v-if="teamBonus"
      color="warning"
      variant="subtle"
      icon="i-heroicons-sparkles"
      class="max-w-xs whitespace-normal text-center"
    >
      {{ teamBonus }}
    </UBadge>
  </div>
</template>

<script setup lang="ts">
import { ACCOUNT_ROLE_CODES } from '@/infrastructure/constants'
import { cnToday, formatCnDate, teamName } from '@/infrastructure/teamEvents'
import type { AccountTeamEvent } from '~/stores/account'

interface Props {
  slotsUsed: number
  slotsTotal: number
  role: string | undefined
  teamEvent?: AccountTeamEvent | null
  // Whether the bonus slot is already in a deal
  bonusSlotInDeal?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  teamEvent: null,
  bonusSlotInDeal: false,
})

const isOnHold = computed(() => props.role === ACCOUNT_ROLE_CODES.onHold)

const slotsOpen = computed(() =>
  Math.max(props.slotsTotal - props.slotsUsed, 0),
)

const statusLabel = computed(() => {
  if (isOnHold.value) return 'Hold'
  if (slotsOpen.value === 0) return 'Full'
  return `${slotsOpen.value} Open`
})

const statusClass = computed(() => {
  if (isOnHold.value) return 'text-gray-400 dark:text-gray-500'
  if (slotsOpen.value === 0) return 'text-green-600 dark:text-green-400'
  return 'text-blue-600 dark:text-blue-400'
})

const badgeColor = computed(() => {
  if (isOnHold.value) return 'neutral'
  if (slotsOpen.value === 0) return 'success'
  return 'info'
})

const description = computed(() => {
  if (isOnHold.value) {
    return 'Your account is on hold and cannot participate in VEP. If you feel this is an error, please contact VEP management for support.'
  }
  if (slotsOpen.value === 0) {
    return 'All of your slots are in active deals. Keep your deals active by exchanging aid with your partners.'
  }
  return `You have ${slotsOpen.value} open slot${slotsOpen.value === 1 ? '' : 's'}. VEP matches open slots with new partners daily, so check back tomorrow.`
})

const teamBonus = computed(() => {
  const event = props.teamEvent
  if (isOnHold.value || !event) return null

  const base = `Includes 1 bonus slot from the ${teamName(event.team)} team's event, in effect through ${formatCnDate(event.endsOn)}.`
  if (props.bonusSlotInDeal) {
    return `${base} The deal using it is temporary and ends with the event.`
  }
  // Matching stops early so every aid offer in the deal fits before the event ends
  if (event.bonusSlotMatchableUntil.split('T')[0] < cnToday()) {
    return `${base} It's too close to the end of the event for VEP to start a new deal in it.`
  }
  return `${base} VEP can match it into a temporary deal until ${formatCnDate(event.bonusSlotMatchableUntil)}.`
})
</script>
