<template>
  <UCard class="shadow-lg">
    <div class="flex flex-col sm:flex-row sm:items-center gap-6">
      <div class="flex-grow space-y-1">
        <p
          class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400"
        >
          Your standing
        </p>
        <h2 class="text-2xl font-bold text-gray-900 dark:text-white">
          {{ summary.title }}
        </h2>
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ summary.detail }}
        </p>
      </div>
      <div class="flex-shrink-0">
        <LeaderboardScore :standing="standing" />
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { getStandingSummary } from '~/infrastructure/leaderboard'
import type { Leaderboard, LeaderboardStanding } from '~/stores/leaderboard'

const props = defineProps<{
  leaderboard: Leaderboard
  standing: LeaderboardStanding
}>()

const summary = computed(() =>
  getStandingSummary(props.leaderboard, props.standing),
)
</script>
