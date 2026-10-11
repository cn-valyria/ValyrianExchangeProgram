<template>
  <UCard :class="cardClasses">
    <div class="relative text-center space-y-4">
      <!-- Medal Icon -->
      <div class="flex justify-center">
        <div :class="medalClasses">
          <Icon :name="medalIcon" class="h-10 w-10" />
        </div>
      </div>

      <!-- Rank Badge -->
      <div class="flex justify-center">
        <UBadge
          :color="badgeColor"
          size="lg"
          variant="solid"
          class="text-lg font-bold px-4 py-2"
        >
          #{{ rank }}
        </UBadge>
      </div>

      <!-- Champion Crown (for 1st place) -->
      <div
        v-if="isChampion"
        class="absolute -top-8 left-1/2 transform -translate-x-1/2"
      >
        <Icon
          name="heroicons:sparkles"
          class="h-8 w-8 text-yellow-300 animate-pulse"
        />
      </div>

      <!-- Account Info -->
      <div class="space-y-2">
        <h3 :class="nameSizeClass">
          {{ account.nationName }}
        </h3>
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ account.rulerName }}
        </p>
        <p class="text-xs text-gray-500 dark:text-gray-500">
          {{ account.allianceName }}
        </p>
      </div>

      <!-- Divider -->
      <div class="border-t border-gray-200 dark:border-gray-700 my-4"></div>

      <!-- Activity Score -->
      <div class="space-y-2">
        <AccountActivityScore
          :score="account.avgDaysToClearDebt"
          :total-times-in-debt="account.totalTimesInDebt"
        />
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { TopScorerAccount } from '~/stores/shared/topScorerAccount'

interface Props {
  account: TopScorerAccount
  rank: string
  medalColor: 'gold' | 'silver' | 'bronze'
  isChampion?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  isChampion: false,
})

const cardClasses = computed(() => {
  const baseClasses = 'transition-all duration-300 hover:scale-105'
  if (props.isChampion) {
    return `${baseClasses} ring-4 ring-yellow-400 dark:ring-yellow-500 shadow-2xl`
  }
  return `${baseClasses} shadow-lg`
})

const medalClasses = computed(() => {
  const baseClasses = 'h-16 w-16 rounded-full flex items-center justify-center'
  switch (props.medalColor) {
    case 'gold':
      return `${baseClasses} bg-gradient-to-br from-yellow-300 to-yellow-500 text-yellow-900`
    case 'silver':
      return `${baseClasses} bg-gradient-to-br from-gray-300 to-gray-400 text-gray-900`
    case 'bronze':
      return `${baseClasses} bg-gradient-to-br from-orange-400 to-orange-600 text-orange-900`
    default:
      return baseClasses
  }
})

const medalIcon = computed(() => {
  switch (props.rank) {
    case '1':
      return 'heroicons:trophy'
    case '2':
      return 'heroicons:star'
    case '3':
      return 'heroicons:shield-check'
    default:
      return 'heroicons:star'
  }
})

const badgeColor = computed(() => {
  switch (props.medalColor) {
    case 'gold':
      return 'warning'
    case 'silver':
      return 'neutral'
    case 'bronze':
      return 'warning'
    default:
      return 'primary'
  }
})

const nameSizeClass = computed(() => {
  if (props.isChampion) {
    return 'text-2xl font-bold text-gray-900 dark:text-white'
  }
  return 'text-xl font-bold text-gray-900 dark:text-white'
})
</script>
