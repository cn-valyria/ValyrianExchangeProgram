<template>
  <UCard class="hover:shadow-lg transition-shadow duration-300">
    <div class="flex items-center gap-6">
      <!-- Rank Badge -->
      <div class="flex-shrink-0">
        <div class="h-16 w-16 rounded-full bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
          <span class="text-2xl font-bold text-primary-600 dark:text-primary-400">
            {{ account.rank }}
          </span>
        </div>
      </div>

      <!-- Account Info -->
      <div class="flex-grow space-y-1">
        <h3 class="text-lg font-bold text-gray-900 dark:text-white">
          {{ account.nationName }}
        </h3>
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ account.rulerName }}
        </p>
        <p class="text-xs text-gray-500 dark:text-gray-500">
          {{ account.allianceName }}
        </p>
      </div>

      <!-- Activity Score Display -->
      <div class="flex-shrink-0">
        <AccountActivityScore
          :score="account.avgDaysToClearDebt"
          :total-times-in-debt="account.totalTimesInDebt"
          compact
        />
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
interface Props {
  account: {
    id: number
    nationId: number
    nationName: string
    rulerName: string
    allianceName: string
    totalTimesInDebt: number
    avgDaysToClearDebt: number
    rank: number
  }
}

defineProps<Props>()
</script>
