<template>
  <div :class="compact ? 'text-right' : 'text-center space-y-1'">
    <p
      :class="[
        'font-bold',
        compact ? 'text-base' : 'text-xl',
        standing.flags === 0
          ? 'text-success-600 dark:text-success-400'
          : 'text-gray-900 dark:text-white',
      ]"
    >
      {{ formatFlagsPerLeg(standing) }}
    </p>
    <p class="text-sm text-gray-600 dark:text-gray-400">
      {{ describeRecord(standing) }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { describeRecord, formatFlagsPerLeg } from '~/infrastructure/leaderboard'
import type { LeaderboardStanding } from '~/stores/leaderboard'

withDefaults(
  defineProps<{ standing: LeaderboardStanding; compact?: boolean }>(),
  {
    compact: false,
  },
)
</script>
