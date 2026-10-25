<template>
  <div class="flex flex-wrap gap-x-4 gap-y-1">
    <span
      v-for="bucket in DEAL_HEALTH_ORDER"
      :key="bucket"
      class="flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-300"
      :title="DEAL_HEALTH_INFO[bucket].detail"
    >
      <span class="size-2.5 rounded-sm" :class="DEAL_HEALTH_COLORS[bucket]" />
      {{ DEAL_HEALTH_INFO[bucket].label }}
    </span>
  </div>
</template>

<script setup lang="ts">
import {
  DEAL_HEALTH_COLORS,
  DEAL_HEALTH_INFO,
  DEAL_HEALTH_ORDER,
} from '~/infrastructure/dashboard'
</script>
