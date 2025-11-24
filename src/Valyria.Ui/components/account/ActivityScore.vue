<template>
  <div class="flex flex-col items-end gap-2 min-w-fit">
    <!-- Score Not Available State -->
    <div v-if="!hasScore" class="flex flex-col items-end gap-2">
      <div class="flex items-center gap-3">
        <div class="text-center">
          <div class="text-3xl font-bold text-gray-400 dark:text-gray-500">
            --
          </div>
          <div class="text-sm font-medium text-gray-600 dark:text-gray-400">
            Activity Score
          </div>
        </div>
        <UBadge color="neutral" size="md" variant="subtle" class="shrink-0">
          Calculating
        </UBadge>
      </div>
      <div class="text-xs text-gray-600 dark:text-gray-400 text-center max-w-xs">
        Your Activity Score will be calculated once you've had debt at least one time.
      </div>
    </div>

    <!-- Score Available State -->
    <div v-else class="flex flex-col items-center gap-2">
      <div class="flex items-center gap-3">
        <div class="text-center">
          <div class="text-3xl font-bold" :class="tierClass">
            {{ formattedScore }}
          </div>
          <div class="text-sm font-medium text-gray-600 dark:text-gray-400">
            Activity Score
          </div>
        </div>
        <UBadge :color="tierBadgeColor" size="md" variant="subtle" class="shrink-0">
          {{ tierName }}
        </UBadge>
      </div>
      <div class="text-xs text-gray-600 dark:text-gray-400 text-center max-w-xs">
        {{ tierDescription }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  score?: number | null
  totalTimesInDebt?: number
}

const props = defineProps<Props>()

// Determine if the account has a score (only if they've had debt at least once)
const hasScore = computed(() => {
  return props.totalTimesInDebt !== undefined && props.totalTimesInDebt > 0
})

// Format the score to 2 decimal places
const formattedScore = computed(() => {
  if (!hasScore.value || props.score === null || props.score === undefined) {
    return '--'
  }
  return props.score.toFixed(2)
})

// Determine the tier based on the score
const tier = computed(() => {
  if (!hasScore.value || props.score === null || props.score === undefined) {
    return null
  }

  const score = props.score

  if (score >= 0 && score < 1) {
    return 'elite'
  } else if (score >= 1 && score < 2) {
    return 'solid'
  } else if (score >= 2 && score < 5) {
    return 'moderate'
  } else if (score >= 5 && score < 10) {
    return 'poor'
  } else {
    return 'terrible'
  }
})

const tierName = computed(() => {
  switch (tier.value) {
    case 'elite': return 'Elite'
    case 'solid': return 'Solid'
    case 'moderate': return 'Moderate'
    case 'poor': return 'Poor'
    case 'terrible': return 'Terrible'
    default: return 'Calculating'
  }
})

const tierClass = computed(() => {
  switch (tier.value) {
    case 'elite': return 'text-purple-600 dark:text-purple-400'
    case 'solid': return 'text-green-600 dark:text-green-400'
    case 'moderate': return 'text-blue-600 dark:text-blue-400'
    case 'poor': return 'text-orange-600 dark:text-orange-400'
    case 'terrible': return 'text-red-600 dark:text-red-400'
    default: return 'text-gray-400 dark:text-gray-500'
  }
})

const tierBadgeColor = computed(() => {
  switch (tier.value) {
    case 'elite': return 'tertiary'
    case 'solid': return 'success'
    case 'moderate': return 'secondary'
    case 'poor': return 'warning'
    case 'terrible': return 'error'
    default: return 'neutral'
  }
})

const tierDescription = computed(() => {
  switch (tier.value) {
    case 'elite':
      return 'Outstanding! You clear your debt in less than a day on average. You are among the best performers in VEP.'
    case 'solid':
      return 'Great work! You clear your debt in 1-2 days on average. Keep up the good performance!'
    case 'moderate':
      return 'You clear your debt in 2-5 days on average. There is room for improvement in your payment speed.'
    case 'poor':
      return 'You clear your debt in 5-10 days on average. Consider clearing your debt more frequently.'
    case 'terrible':
      return 'You take more than 10 days to clear your debt on average. Please prioritize clearing your debt more quickly.'
    default:
      return ''
  }
})
</script>
