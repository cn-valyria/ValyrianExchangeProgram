<template>
  <div>
    <div class="flex h-3 gap-0.5">
      <div
        v-for="bucket in buckets"
        :key="bucket"
        class="h-3 first:rounded-l last:rounded-r"
        :class="DEAL_HEALTH_COLORS[bucket]"
        :style="{ width: `${(health[bucket] / total) * 100}%` }"
        :title="`${DEAL_HEALTH_INFO[bucket].label}: ${health[bucket]}`"
      />
    </div>
    <p class="text-xs text-gray-600 dark:text-gray-300 mt-1">
      {{
        buckets
          .map((b) => `${health[b]} ${DEAL_HEALTH_INFO[b].label.toLowerCase()}`)
          .join(' · ')
      }}
    </p>
  </div>
</template>

<script setup lang="ts">
import type { DealHealth } from '~/stores/admin'
import {
  DEAL_HEALTH_COLORS,
  DEAL_HEALTH_INFO,
  DEAL_HEALTH_ORDER,
} from '~/infrastructure/dashboard'

interface Props {
  health: Record<DealHealth, number>
}

const props = defineProps<Props>()

const buckets = computed(() =>
  DEAL_HEALTH_ORDER.filter((b) => props.health[b] > 0),
)

const total = computed(() =>
  DEAL_HEALTH_ORDER.reduce((sum, b) => sum + props.health[b], 0),
)
</script>
