<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">List Performance History</h2>
        <UBadge color="secondary" size="lg" variant="subtle">
          {{ listPerformance.length }} {{ listPerformance.length === 1 ? 'entry' : 'entries' }}
        </UBadge>
      </div>
    </template>

    <div class="space-y-4">
      <UAlert
        color="info"
        icon="i-heroicons-information-circle"
        title="About Your List Performance"
        description="This performance history is driving your activity score. Each entry starts with the day you were added to an outgoing aid list, and shows the number of days it took to remove you from that list."
      />

      <div v-if="listPerformance.length === 0" class="text-center py-8 text-gray-600 dark:text-gray-400">
        No list performance history available yet.
      </div>

      <div v-else class="space-y-3">
        <div
          v-for="(item, index) in listPerformance"
          :key="index"
          class="flex items-center justify-between p-4 rounded-lg border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
        >
          <!-- Left side: Date started -->
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 mb-1">
              <span class="text-sm text-gray-600 dark:text-gray-400 truncate">
                Started:&nbsp;
              </span>
              <span class="text-xl font-bold text-gray-900 dark:text-white">
                {{ formatDate(item.dayStarted) }}
              </span>
            </div>
          </div>

          <!-- Right side: Days until removed -->
          <div class="flex items-center gap-3 ml-4">
            <div class="text-sm text-gray-600 dark:text-gray-400 truncate">
              In {{ item.listType }} List For:&nbsp;
            </div>
            <div class="text-right">
              <div class="text-2xl font-bold" :class="getDaysColorClass(item.daysUntilRemoved)">
                {{ item.daysUntilRemoved }} days
              </div>
            </div>
            <UBadge
              :color="getDaysBadgeColor(item.daysUntilRemoved)"
              size="md"
              variant="subtle"
              class="shrink-0"
            >
              {{ getDaysTier(item.daysUntilRemoved) }}
            </UBadge>
          </div>
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { ListPerformance } from '~/stores/shared/listPerformance'

interface Props {
  listPerformance: ListPerformance[]
}

const props = withDefaults(defineProps<Props>(), {
  listPerformance: () => []
})

// Format date to readable format
const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return date.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}

// Determine the tier based on days (matching ActivityScore tiers)
const getDaysTier = (days: number): string => {
  if (days >= 0 && days < 1) {
    return 'Elite'
  } else if (days >= 1 && days < 2) {
    return 'Solid'
  } else if (days >= 2 && days < 5) {
    return 'Moderate'
  } else if (days >= 5 && days < 10) {
    return 'Poor'
  } else {
    return 'Terrible'
  }
}

// Get color class for days text (matching ActivityScore colors)
const getDaysColorClass = (days: number): string => {
  if (days >= 0 && days < 1) {
    return 'text-purple-600 dark:text-purple-400'
  } else if (days >= 1 && days < 2) {
    return 'text-green-600 dark:text-green-400'
  } else if (days >= 2 && days < 5) {
    return 'text-blue-600 dark:text-blue-400'
  } else if (days >= 5 && days < 10) {
    return 'text-orange-600 dark:text-orange-400'
  } else {
    return 'text-red-600 dark:text-red-400'
  }
}

// Get badge color for days (matching ActivityScore badge colors)
const getDaysBadgeColor = (days: number): 'tertiary' | 'success' | 'secondary' | 'warning' | 'error' => {
  if (days >= 0 && days < 1) {
    return 'tertiary'
  } else if (days >= 1 && days < 2) {
    return 'success'
  } else if (days >= 2 && days < 5) {
    return 'secondary'
  } else if (days >= 5 && days < 10) {
    return 'warning'
  } else {
    return 'error'
  }
}
</script>
